using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace EventHub;

public class PlatformWorker(IServiceScopeFactory scopes, IHubContext<AvailabilityHub> hub, IConfiguration config, ILogger<PlatformWorker> log) : BackgroundService
{
    IConnection? rabbit; IChannel? channel;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<Store>();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
                await store.Write(async db => { await BookingService.Expire(db); return true; });
                await using var read = await factory.CreateDbContextAsync();
                Telemetry.ActiveHolds.Set(await read.Holds.CountAsync(x => x.Status == "ACTIVE" && x.ExpiresAt > DateTime.UtcNow, stoppingToken));
                Telemetry.PendingPayments.Set(await read.Payments.CountAsync(x => x.Status == "UNKNOWN" || x.Status == "UNKNOWN_CAPTURE" || x.Status == "ALLOCATED" || x.Status == "CAPTURED_UNALLOCATED", stoppingToken));
                Telemetry.OutboxPending.Set(await read.Outbox.CountAsync(x => x.PublishedAt == null, stoppingToken));
                var payments = await read.Payments.AsNoTracking().Where(x => x.Status != "CAPTURED" && x.Status != "FAILED" && x.Status != "VOIDED" && x.Status != "REFUNDED" && x.Status != "CAPTURED_UNALLOCATED").OrderBy(x => x.UpdatedAt).Take(20).Select(x => x.OrderId).ToListAsync(stoppingToken);
                foreach (var id in payments)
                    await scope.ServiceProvider.GetRequiredService<PaymentService>().Reconcile(id);
                var events = await read.Outbox.AsNoTracking().Where(x => x.PublishedAt == null).OrderBy(x => x.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var ev in events)
                {
                    try
                    {
                        if (ev.Type == "seats.changed")
                            await hub.Clients.Group("event-" + ev.AggregateId).SendAsync("SeatsChanged", System.Text.Json.JsonDocument.Parse(ev.Payload).RootElement, stoppingToken);
                        if (!string.IsNullOrEmpty(config["RabbitMQ:Url"]))
                        {
                            if (rabbit == null || !rabbit.IsOpen || channel == null || !channel.IsOpen)
                            {
                                var cf = new ConnectionFactory { Uri = new Uri(config["RabbitMQ:Url"]!) };
                                rabbit = await cf.CreateConnectionAsync(stoppingToken);
                                channel = await rabbit.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), stoppingToken);
                                await channel.ExchangeDeclareAsync("eventhub", "topic", durable: true, cancellationToken: stoppingToken);
                                await channel.QueueDeclareAsync("eventhub.events", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                                await channel.QueueBindAsync("eventhub.events", "eventhub", "#", cancellationToken: stoppingToken);
                            }
                            await channel.BasicPublishAsync("eventhub", ev.Type, mandatory: true, new BasicProperties { Persistent = true, MessageId = ev.Id.ToString(), ContentType = "application/json" }, Encoding.UTF8.GetBytes(ev.Payload), stoppingToken);
                        }
                        await store.Write(async db => { var e = await db.Outbox.SingleAsync(x => x.Id == ev.Id); e.PublishedAt = DateTime.UtcNow; e.Attempts++; return true; });
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { log.LogWarning("Outbox event {Id} retained: {Kind}", ev.Id, ex.GetType().Name); break; }
                }
                if (!string.IsNullOrEmpty(config["Smtp:Host"]))
                {
                    var notes = await read.Notifications.AsNoTracking().Where(x => x.EmailedAt == null).OrderBy(x => x.Id).Take(10).ToListAsync(stoppingToken);
                    foreach (var note in notes)
                    {
                        var user = await read.Users.AsNoTracking().SingleAsync(x => x.Id == note.UserId, stoppingToken);
                        using var smtp = new SmtpClient(config["Smtp:Host"], config.GetValue("Smtp:Port", 587)) { EnableSsl = config.GetValue("Smtp:EnableSsl", true) };
                        if (!string.IsNullOrEmpty(config["Smtp:User"]))
                            smtp.Credentials = new NetworkCredential(config["Smtp:User"], config["Smtp:Password"]);
                        await smtp.SendMailAsync(new MailMessage(config["Smtp:From"] ?? "tickets@eventhub.example", user.Email, note.Title, note.Body), stoppingToken);
                        await store.Write(async db => { var n = await db.Notifications.SingleAsync(x => x.Id == note.Id); n.EmailedAt = DateTime.UtcNow; return true; });
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { log.LogError("Background cycle failed: {Kind}", ex.GetType().Name); }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) { break; }
        }
        if (channel != null)
            await channel.DisposeAsync();
        if (rabbit != null)
            await rabbit.DisposeAsync();
    }
}
