using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EventHub;

public record PaymentResult(string Status, string Reference, string? ClientSecret = null, string? Error = null);
public interface IPaymentProvider
{
    string Name
    {
        get;
    }
    Task<PaymentResult> Authorize(PaymentAttempt attempt, Order order);
    Task<PaymentResult> Capture(PaymentAttempt attempt);
    Task<PaymentResult> Get(PaymentAttempt attempt);
    Task Void(PaymentAttempt attempt);
    Task<string> Refund(PaymentAttempt attempt, string refundId, int amount);
}
public class SandboxProvider : IPaymentProvider
{
    public string Name => "Sandbox";
    public Task<PaymentResult> Authorize(PaymentAttempt a, Order o) => Task.FromResult(a.Outcome == "decline" ? new PaymentResult("FAILED", a.Id, Error: "The sandbox payment was declined.") : new PaymentResult("AUTHORIZED", a.Id));
    public Task<PaymentResult> Capture(PaymentAttempt a) => Task.FromResult(a.Outcome == "delayed" && DateTime.UtcNow - a.CreatedAt < TimeSpan.FromSeconds(8) ? new PaymentResult("UNKNOWN", a.Id) : new PaymentResult("CAPTURED", a.Id));
    public Task<PaymentResult> Get(PaymentAttempt a) => Capture(a);
    public Task Void(PaymentAttempt a) => Task.CompletedTask;
    public Task<string> Refund(PaymentAttempt a, string id, int amount) => Task.FromResult("sandbox_refund_" + id);
}
public class StripeProvider(HttpClient http, IConfiguration config) : IPaymentProvider
{
    public string Name => "Stripe";
    async Task<JsonElement> Call(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? values = null, string? key = null)
    {
        using var req = new HttpRequestMessage(method, "https://api.stripe.com/v1/" + path);
        var secret = config["Payments:StripeSecretKey"] ?? throw new InvalidOperationException("Stripe secret key missing.");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (key != null)
            req.Headers.Add("Idempotency-Key", key);
        if (values != null)
            req.Content = new FormUrlEncodedContent(values);
        using var res = await http.SendAsync(req);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();
        if (!res.IsSuccessStatusCode)
        {
            if ((int)res.StatusCode >= 500 || res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("Payment provider temporarily unavailable.");
            throw new ApiError(402, "PAYMENT_ERROR", data.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg) ? msg.GetString()! : "The payment provider rejected this request.");
        }
        return data;
    }
    static PaymentResult Result(JsonElement p)
    {
        var status = p.GetProperty("status").GetString();
        return new(status switch
        {
            "succeeded" => "CAPTURED",
            "requires_capture" => "AUTHORIZED",
            "canceled" => "VOIDED",
            "requires_payment_method" or "requires_action" or "requires_confirmation" => "REQUIRES_ACTION",
            _ => "UNKNOWN"
        }, p.GetProperty("id").GetString()!, p.TryGetProperty("client_secret", out var s) ? s.GetString() : null);
    }
    public async Task<PaymentResult> Authorize(PaymentAttempt a, Order o)
    {
        if (!string.IsNullOrEmpty(a.Reference))
            return await Get(a);
        var values = new Dictionary<string, string> { { "amount", o.Total.ToString() }, { "currency", o.Currency.ToLowerInvariant() }, { "capture_method", "manual" }, { "payment_method_types[]", "card" }, { "metadata[orderId]", o.Id } };
        return Result(await Call(HttpMethod.Post, "payment_intents", values, "authorize-" + a.Id));
    }
    public async Task<PaymentResult> Capture(PaymentAttempt a) => Result(await Call(HttpMethod.Post, $"payment_intents/{Uri.EscapeDataString(a.Reference)}/capture", [], "capture-" + a.Id));
    public async Task<PaymentResult> Get(PaymentAttempt a) => Result(await Call(HttpMethod.Get, $"payment_intents/{Uri.EscapeDataString(a.Reference)}"));
    public async Task Void(PaymentAttempt a)
    {
        await Call(HttpMethod.Post, $"payment_intents/{Uri.EscapeDataString(a.Reference)}/cancel", [], "void-" + a.Id);
    }
    public async Task<string> Refund(PaymentAttempt a, string id, int amount)
    {
        var result = await Call(HttpMethod.Post, "refunds", new Dictionary<string, string> { { "payment_intent", a.Reference }, { "amount", amount.ToString() } }, "refund-" + id);
        if (result.GetProperty("status").GetString() != "succeeded")
            throw new ApiError(409, "REFUND_PENDING", "Provider has not confirmed the refund. Reconcile again after settlement.");
        return result.GetProperty("id").GetString()!;
    }
}
public record PayRequest(string Outcome = "success");
public class PaymentService(Store store, IDbContextFactory<AppDb> factory, IPaymentProvider provider, ILogger<PaymentService> logger)
{
    public async Task<object> Start(string user, string orderId, string key, PayRequest req)
    {
        if (req.Outcome is not ("success" or "decline" or "delayed"))
            throw new ApiError(400, "INVALID_OUTCOME", "Invalid sandbox outcome.");
        await store.Idempotent(user, "payment", key, new
        {
            orderId,
            req.Outcome
        }, async db =>
        {
            var o = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == user) ?? throw new ApiError(404, "ORDER_NOT_FOUND", "Order not found.");
            var existing = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == o.Id);
            if (existing != null)
                return new
                {
                    orderId = o.Id,
                    attemptId = existing.Id
                };
            if (o.Status != "PENDING")
                throw new ApiError(409, "ORDER_NOT_PAYABLE", "This order is not awaiting payment.");
            var h = await db.Holds.SingleAsync(x => x.Id == o.HoldId);
            if (h.Status != "ACTIVE" || h.ExpiresAt <= DateTime.UtcNow)
                throw new ApiError(410, "HOLD_EXPIRED", "The reservation expired. Choose your seats again.");
            var a = new PaymentAttempt { OrderId = o.Id, Provider = provider.Name, Outcome = provider.Name == "Sandbox" ? req.Outcome : "success" };
            db.Payments.Add(a);
            o.Status = "PROCESSING";
            Store.Audit(db, user, "payment.started", o.Id);
            return new
            {
                orderId = o.Id,
                attemptId = a.Id
            };
        });
        await Reconcile(orderId);
        return await View(user, orderId);
    }
    public async Task<object> View(string user, string orderId, bool admin = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId && (admin || x.UserId == user)) ?? throw new ApiError(404, "ORDER_NOT_FOUND", "Order not found.");
        var ev = await db.Events.SingleAsync(x => x.Id == order.EventId);
        var venue = await db.Venues.SingleAsync(x => x.Id == ev.VenueId);
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == order.Id);
        var seats = await (from hs in db.HoldSeats join s in db.Seats on new { hs.EventId, Id = hs.SeatId } equals new { s.EventId, s.Id } where hs.HoldId == order.HoldId select new { s.Id, s.Row, s.Number, s.Section, hs.Price }).ToListAsync();
        var tickets = await db.Tickets.Where(x => x.OrderId == order.Id && x.UserId == user).ToListAsync();
        var refund = await db.Refunds.SingleOrDefaultAsync(x => x.OrderId == order.Id);
        return new
        {
            order,
            ev,
            venue,
            seats,
            tickets,
            refund,
            payment = payment == null ? null : new
            {
                payment.Id,
                payment.Status,
                payment.Provider,
                payment.Error,
                clientSecret = admin ? null : payment.ClientSecret
            }
        };
    }
    public async Task Reconcile(string orderId)
    {
        await using var read = await factory.CreateDbContextAsync();
        var a = await read.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId);
        if (a == null || a.Status is "CAPTURED" or "FAILED" or "VOIDED" or "REFUNDED")
            return;
        var o = await read.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        PaymentResult result;
        try
        {
            result = a.Status is "ALLOCATED" or "UNKNOWN_CAPTURE" ? await provider.Get(a) : await provider.Authorize(a, o);
            if (result.Status == "REQUIRES_ACTION")
            {
                await SaveStatus(a.Id, result);
                var h = await read.Holds.AsNoTracking().SingleAsync(x => x.Id == o.HoldId);
                if (h.Status != "ACTIVE" || h.ExpiresAt <= DateTime.UtcNow)
                {
                    a.Reference = result.Reference;
                    await provider.Void(a);
                    await Fail(orderId, "VOIDED", "The reservation expired before payment authorization.");
                }
                return;
            }
            if (result.Status is "FAILED" or "VOIDED")
            {
                await Fail(orderId, result.Status, result.Error ?? "Payment did not complete.");
                return;
            }
            if (result.Status == "UNKNOWN")
            {
                await SaveStatus(a.Id, result, a.Status is "ALLOCATED" or "UNKNOWN_CAPTURE" ? "UNKNOWN_CAPTURE" : "UNKNOWN");
                return;
            }
            a.Reference = result.Reference;
            if (result.Status != "CAPTURED")
                await SaveStatus(a.Id, result);
            if (result.Status == "AUTHORIZED")
            {
                var allocated = await Allocate(orderId);
                if (!allocated)
                {
                    await provider.Void(a);
                    await Fail(orderId, "VOIDED", "The reservation expired before payment authorization.");
                    return;
                }
                a.Status = "ALLOCATED";
                result = await provider.Capture(a);
                if (result.Status == "UNKNOWN")
                {
                    await SaveStatus(a.Id, result, "UNKNOWN_CAPTURE");
                    return;
                }
                if (result.Status != "CAPTURED")
                {
                    await Fail(orderId, "FAILED", result.Error ?? "Payment capture failed.");
                    return;
                }
            }
            if (result.Status == "CAPTURED")
                await Complete(orderId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Payment {OrderId} needs reconciliation ({Kind})", orderId, ex.GetType().Name);
            // Keep ownership and the attempt. A retry uses the exact same provider idempotency key.
        }
        catch (ApiError ex)
        {
            logger.LogWarning("Payment {OrderId}: {Code}", orderId, ex.Code);
            // Provider errors after allocation can be ambiguous. Never release allocated seats here.
            if (a.Status is not ("ALLOCATED" or "UNKNOWN_CAPTURE"))
                await Fail(orderId, "FAILED", ex.Message);
        }
    }
    Task<bool> SaveStatus(string id, PaymentResult r, string? status = null) => store.Write(async db =>
    {
        var a = await db.Payments.SingleAsync(x => x.Id == id);
        if (a.Status is "CAPTURED" or "FAILED" or "VOIDED" or "REFUNDED")
            return true;
        a.Reference = r.Reference;
        a.ClientSecret = r.ClientSecret ?? a.ClientSecret;
        // Do not downgrade an allocation when another reconciler reads an authorization.
        if (a.Status is not ("ALLOCATED" or "UNKNOWN_CAPTURE") || r.Status == "CAPTURED" || status == "UNKNOWN_CAPTURE")
            a.Status = status ?? r.Status;
        a.UpdatedAt = DateTime.UtcNow;
        return true;
    });
    Task<bool> Allocate(string id) => store.Write(async db =>
    {
        var o = await db.Orders.SingleAsync(x => x.Id == id);
        var h = await db.Holds.SingleAsync(x => x.Id == o.HoldId);
        if (h.Status == "CONVERTED" || h.Status == "ALLOCATED")
            return true;
        if (h.Status != "ACTIVE" || h.ExpiresAt <= DateTime.UtcNow)
            return false;
        var seats = await db.Seats.Where(x => x.HoldId == h.Id).OrderBy(x => x.Id).ToListAsync();
        var expected = await db.HoldSeats.CountAsync(x => x.HoldId == h.Id);
        if (seats.Count != expected || seats.Any(x => x.Status != "HELD"))
            return false;
        foreach (var s in seats)
        {
            s.Status = "ALLOCATED";
            s.ExpiresAt = null;
            s.Version++;
        }
        h.Status = "ALLOCATED";
        var a = await db.Payments.SingleAsync(x => x.OrderId == id);
        a.Status = "ALLOCATED";
        Store.Emit(db, "seats.changed", h.EventId.ToString(), new
        {
            eventId = h.EventId,
            seats = seats.Select(BookingService.SeatDto)
        });
        return true;
    });
    Task<bool> Complete(string id) => store.Write(async db =>
    {
        var o = await db.Orders.SingleAsync(x => x.Id == id);
        if (o.Status == "CONFIRMED")
            return true;
        var h = await db.Holds.SingleAsync(x => x.Id == o.HoldId);
        var a = await db.Payments.SingleAsync(x => x.OrderId == id);
        // A captured payment without an allocation is an exception for manual reconciliation.
        if (h.Status != "ALLOCATED")
        {
            a.Status = "CAPTURED_UNALLOCATED";
            Store.Audit(db, "system", "payment.exception", id, "Captured payment without seat allocation; refund required.");
            return false;
        }
        var seats = await db.Seats.Where(x => x.HoldId == h.Id && x.Status == "ALLOCATED").OrderBy(x => x.Id).ToListAsync();
        if (seats.Count != await db.HoldSeats.CountAsync(x => x.HoldId == h.Id))
            throw new ApiError(409, "ALLOCATION_MISMATCH", "Your payment needs manual reconciliation.");
        foreach (var s in seats)
        {
            s.Status = "SOLD";
            s.Version++;
            db.Tickets.Add(new()
            {
                OrderId = o.Id,
                UserId = o.UserId,
                EventId = o.EventId,
                SeatId = s.Id
            });
        }
        o.Status = "CONFIRMED";
        h.Status = "CONVERTED";
        a.Status = "CAPTURED";
        a.ClientSecret = null;
        a.UpdatedAt = DateTime.UtcNow;
        Store.Emit(db, "seats.changed", o.EventId.ToString(), new
        {
            eventId = o.EventId,
            seats = seats.Select(BookingService.SeatDto)
        });
        Store.Notify(db, o.UserId, "You’re going!", "Your booking is confirmed. Your tickets are ready in My tickets.");
        Store.Audit(db, "system", "order.confirmed", id);
        return true;
    });
    Task<bool> Fail(string id, string status, string error) => store.Write(async db =>
    {
        var o = await db.Orders.SingleAsync(x => x.Id == id);
        if (o.Status == "CONFIRMED")
            return true;
        var a = await db.Payments.SingleAsync(x => x.OrderId == id);
        a.Status = status;
        a.Error = error;
        a.ClientSecret = null;
        o.Status = "FAILED";
        var h = await db.Holds.SingleAsync(x => x.Id == o.HoldId);
        h.Status = "RELEASED";
        var seats = await db.Seats.Where(x => x.HoldId == h.Id && (x.Status == "HELD" || x.Status == "ALLOCATED")).ToListAsync();
        foreach (var s in seats)
        {
            s.Status = "AVAILABLE";
            s.HoldId = null;
            s.ExpiresAt = null;
            s.Version++;
        }
        Store.Emit(db, "seats.changed", h.EventId.ToString(), new
        {
            eventId = h.EventId,
            seats = seats.Select(BookingService.SeatDto)
        });
        Store.Audit(db, "system", "payment.failed", id, error);
        return true;
    });
    public Task<object> RequestRefund(string user, string id, string reason) => store.Write<object>(async db =>
    {
        Security.CheckText(reason, 5, 500, "Reason");
        var o = await db.Orders.SingleOrDefaultAsync(x => x.Id == id && x.UserId == user) ?? throw new ApiError(404, "ORDER_NOT_FOUND", "Order not found.");
        var previous = await db.Refunds.SingleOrDefaultAsync(x => x.OrderId == id);
        if (previous != null)
            return previous;
        if (o.Status != "CONFIRMED")
            throw new ApiError(409, "REFUND_NOT_ALLOWED", "Only confirmed bookings can be refunded.");
        var ev = await db.Events.SingleAsync(x => x.Id == o.EventId);
        if (ev.StartsAt <= DateTime.UtcNow.AddHours(24))
            throw new ApiError(409, "REFUND_WINDOW_CLOSED", "Refunds close 24 hours before the event.");
        if (await db.Tickets.AnyAsync(x => x.OrderId == id && (x.Status != "VALID" || x.TransferCount > 0)))
            throw new ApiError(409, "REFUND_NOT_ALLOWED", "Transferred or checked-in tickets cannot be refunded.");
        var refund = new Refund { OrderId = id, Amount = o.Total, Reason = reason };
        db.Refunds.Add(refund);
        o.Status = "REFUND_REQUESTED";
        // Freeze transfers/check-in while a refund is pending.
        Store.Audit(db, user, "refund.requested", id, reason);
        return refund;
    });
    public async Task<object> ReviewRefund(string actor, string id, bool approve)
    {
        var refund = await store.Write(async db =>
        {
            var r = await db.Refunds.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiError(404, "REFUND_NOT_FOUND", "Refund not found.");
            if (r.Status is "APPROVED" or "REJECTED")
                return r;
            if (r.Status == "PROCESSING" && !approve)
                throw new ApiError(409, "REFUND_PROCESSING", "A refund already sent to the provider cannot be rejected.");
            var o = await db.Orders.SingleAsync(x => x.Id == r.OrderId);
            if (!approve)
            {
                r.Status = "REJECTED";
                o.Status = "CONFIRMED";
            }
            else
                r.Status = "PROCESSING";
            Store.Audit(db, actor, approve ? "refund.approving" : "refund.rejected", id);
            return r;
        });
        if (refund.Status != "PROCESSING")
            return refund;
        await using var read = await factory.CreateDbContextAsync();
        var a = await read.Payments.AsNoTracking().SingleAsync(x => x.OrderId == refund.OrderId);
        var reference = await provider.Refund(a, refund.Id, refund.Amount);
        return await store.Write<object>(async db =>
        {
            var r = await db.Refunds.SingleAsync(x => x.Id == id);
            if (r.Status == "APPROVED")
                return r;
            var o = await db.Orders.SingleAsync(x => x.Id == r.OrderId);
            o.Status = "REFUNDED";
            r.Status = "APPROVED";
            r.ProviderReference = reference;
            var payment = await db.Payments.SingleAsync(x => x.OrderId == o.Id);
            payment.Status = "REFUNDED";
            var tickets = await db.Tickets.Where(x => x.OrderId == o.Id).ToListAsync();
            var ids = tickets.Select(x => x.SeatId).ToArray();
            var seats = await db.Seats.Where(x => x.EventId == o.EventId && ids.Contains(x.Id)).ToListAsync();
            foreach (var t in tickets)
            {
                t.Status = "REFUNDED";
                t.IsActive = false;
                t.TokenVersion++;
            }
            foreach (var s in seats)
            {
                s.Status = "AVAILABLE";
                s.HoldId = null;
                s.ExpiresAt = null;
                s.Version++;
            }
            Store.Emit(db, "seats.changed", o.EventId.ToString(), new
            {
                eventId = o.EventId,
                seats = seats.Select(BookingService.SeatDto)
            });
            Store.Notify(db, o.UserId, "Refund approved", "Your refund has been confirmed by the payment provider.");
            Store.Audit(db, actor, "refund.approved", r.Id);
            return r;
        });
    }
}
