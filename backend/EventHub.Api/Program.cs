using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using EventHub;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using QRCoder;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var provider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var sql = provider == "SqlServer";
var postgres = provider == "Postgres";
if (builder.Configuration["VERCEL"] == "1" && provider == "Sqlite")
    throw new InvalidOperationException("Vercel requires an external database; configure DatabaseProvider and DATABASE_URL.");
if (!sql && !postgres) Directory.CreateDirectory("data");
var port = builder.Configuration["PORT"];
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls("http://0.0.0.0:" + port);
builder.Services.AddDbContextFactory<AppDb, PlatformDbFactory>(o =>
{
    var connection = builder.Configuration.GetConnectionString("Database")!;
    if (sql)
        o.UseSqlServer(connection);
    else if (postgres)
        o.UseNpgsql(DatabaseConfiguration.PostgresConnection(builder.Configuration), n => n.MigrationsHistoryTable("__EFMigrationsHistory", "eventhub"));
    else
        o.UseSqlite(connection);
});
builder.Services.AddScoped<Store>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<TicketSigner>();
builder.Services.AddSingleton<PasswordHasher<User>>();
builder.Services.AddHttpClient<StripeProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<IPaymentProvider>(sp => builder.Configuration["Payments:Provider"] == "Stripe" ? sp.GetRequiredService<StripeProvider>() : new SandboxProvider());
var signalr = builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 4096);
if (!string.IsNullOrEmpty(builder.Configuration["Redis:Connection"])) signalr.AddStackExchangeRedis(builder.Configuration["Redis:Connection"]!, options => options.Configuration.AbortOnConnectFail = false);
if (builder.Configuration.GetValue("Workers:Enabled", true)) builder.Services.AddHostedService<PlatformWorker>();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) => { ctx.HttpContext.Response.Headers.RetryAfter = "60"; await ctx.HttpContext.Response.WriteAsJsonAsync(new { status = 429, code = "RATE_LIMITED", title = "Too many requests. Try again in a minute.", retryable = true }, ct); };
});
var app = builder.Build();
_ = app.Services.GetRequiredService<TicketSigner>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (sql || postgres)
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    if (!sql && !postgres)
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    // Keep initial users, venues, events, and inventory atomic across a cold start.
    var store = scope.ServiceProvider.GetRequiredService<Store>();
    await store.Write(async seedDb => { await Seed.Run(seedDb, builder.Configuration, app.Environment); return true; });
}
app.Use(async (ctx, next) =>
{
    var correlation = Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = correlation;
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (ctx.Request.Path.StartsWithSegments("/api/v1") && !ctx.Request.Path.StartsWithSegments("/api/v1/events"))
        ctx.Response.Headers.CacheControl = "no-store";
    try
    {
        // Browsers set Fetch Metadata; reject cross-site cookie mutations. Explicit Origins can be configured for deployments.
        if (ctx.Request.Method is not ("GET" or "HEAD" or "OPTIONS") && !ctx.Request.Path.StartsWithSegments("/api/v1/webhooks"))
        {
            if (ctx.Request.Headers["Sec-Fetch-Site"] == "cross-site")
                throw new ApiError(403, "CSRF_REJECTED", "Cross-site requests are not allowed.");
            var origin = ctx.Request.Headers.Origin.ToString();
            var allowed = builder.Configuration["PublicOrigin"];
            if (!string.IsNullOrEmpty(origin) && !string.IsNullOrEmpty(allowed) && origin.TrimEnd('/') != allowed.TrimEnd('/'))
                throw new ApiError(403, "CSRF_REJECTED", "This origin is not allowed.");
        }
        var token = ctx.Request.Cookies["eventhub_session"];
        if (token != null)
        {
            var db = ctx.RequestServices.GetRequiredService<AppDb>();
            var hash = Security.Hash(token);
            var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == hash && x.ExpiresAt > DateTime.UtcNow);
            if (session != null)
            {
                var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.UserId);
                if (u != null)
                    ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id), new Claim(ClaimTypes.Role, u.Role) }, "session"));
            }
        }
        await next();
    }
    catch (ApiError e) { ctx.Response.StatusCode = e.Status; await ctx.Response.WriteAsJsonAsync(new { type = "https://eventhub.example/problems/" + e.Code.ToLowerInvariant().Replace('_', '-'), title = e.Message, status = e.Status, code = e.Code, correlationId = correlation, retryable = false, details = e.Details }); }
    catch (BadHttpRequestException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { code = "INVALID_REQUEST", title = "The request body is invalid.", status = 400, correlationId = correlation }); }
    catch (Exception e) { app.Logger.LogError(e, "Request failed {Correlation}", correlation); ctx.Response.StatusCode = 503; await ctx.Response.WriteAsJsonAsync(new { code = "SERVICE_UNAVAILABLE", title = "This service is temporarily unavailable. Please try again.", status = 503, correlationId = correlation, retryable = true }); }
});
app.UseHttpMetrics();
app.UseRateLimiter();
app.MapOpenApi();
app.MapGet("/health", async (AppDb db) =>
{
    var healthy = await db.Database.CanConnectAsync();
    return Results.Json(new
    {
        status = healthy ? "healthy" : "unhealthy",
        database = provider,
        time = DateTime.UtcNow
    }, statusCode: healthy ? 200 : 503);
});
app.MapGet("/metrics", async (HttpContext ctx) =>
{
    if (!app.Environment.IsDevelopment())
    {
        var expected = builder.Configuration["Observability:MetricsToken"];
        var actual = ctx.Request.Headers.Authorization.ToString();
        var valid = !string.IsNullOrEmpty(expected) && actual.Length == expected.Length + 7 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes("Bearer " + expected));
        if (!valid)
            Security.Require(ctx, "Admin");
    }
    ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
    await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ctx.Response.Body);
});
app.MapHub<AvailabilityHub>("/hubs/availability");
var api = app.MapGroup("/api/v1").RequireRateLimiting("api");
api.AddEndpointFilter(async (context, next) =>
{
    var nullability = new System.Reflection.NullabilityInfoContext();
    foreach (var arg in context.Arguments.Where(a => a != null && a.GetType().Name.EndsWith("Request")))
    {
        foreach (var property in arg!.GetType().GetProperties())
        {
            if (!property.PropertyType.IsValueType && nullability.Create(property).ReadState == System.Reflection.NullabilityState.NotNull && property.GetValue(arg) == null)
                throw new ApiError(400, "VALIDATION_ERROR", $"{property.Name} is required.");
        }
    }
    return await next(context);
});
api.MapGet("/config", () => new { paymentProvider = builder.Configuration["Payments:Provider"], stripePublishableKey = builder.Configuration["Payments:StripePublishableKey"], demoAvailable = app.Environment.IsDevelopment(), realtimeMode = !builder.Configuration.GetValue("Workers:Enabled", true) && string.IsNullOrEmpty(builder.Configuration["Redis:Connection"]) ? "snapshot" : "signalr", currency = "BDT", holdSeconds = builder.Configuration.GetValue("Booking:HoldSeconds", 300) });
api.MapGet("/auth/me", async (HttpContext ctx, AppDb db) =>
{
    var id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Results.Ok(new
    {
        user = id == null ? null : Security.Profile(await db.Users.AsNoTracking().SingleAsync(x => x.Id == id))
    });
});
api.MapPost("/auth/register", async (RegisterRequest req, HttpContext ctx, Store store, PasswordHasher<User> hasher) =>
{
    Security.CheckText(req.Name, 2, 100, "Name");
    Security.CheckText(req.Password, 10, 128, "Password");
    var email = ValidEmail(req.Email);
    var user = await store.Write(async db =>
    {
        if (await db.Users.AnyAsync(x => x.Email == email))
            throw new ApiError(409, "EMAIL_TAKEN", "An account already exists with this email.");
        var u = new User { Name = req.Name.Trim(), Email = email };
        u.PasswordHash = hasher.HashPassword(u, req.Password);
        db.Users.Add(u);
        Store.Notify(db, u.Id, "Welcome to EventHub", "Discover your next unforgettable experience.");
        return u;
    });
    await SignIn(ctx, store, user);
    return Results.Json(new
    {
        user = Security.Profile(user)
    }, statusCode: 201);
}).RequireRateLimiting("auth");
api.MapPost("/auth/login", async (LoginRequest req, HttpContext ctx, AppDb db, Store store, PasswordHasher<User> hasher) =>
{
    var email = ValidEmail(req.Email);
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email);
    if (user == null || req.Password.Length > 128 || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        throw new ApiError(401, "INVALID_CREDENTIALS", "Email or password is incorrect.");
    await SignIn(ctx, store, user);
    return new
    {
        user = Security.Profile(user)
    };
}).RequireRateLimiting("auth");
api.MapPost("/auth/logout", async (HttpContext ctx, Store store) =>
{
    var token = ctx.Request.Cookies["eventhub_session"];
    if (token != null)
    {
        var hash = Security.Hash(token);
        await store.Write(async db => { await db.Sessions.Where(x => x.Id == hash).ExecuteDeleteAsync(); return true; });
    }
    ctx.Response.Cookies.Delete("eventhub_session");
    return Results.Ok(new
    {
        ok = true
    });
});
api.MapPut("/account", async (ProfileRequest req, HttpContext ctx, Store store) =>
{
    var user = Security.Require(ctx);
    Security.CheckText(req.Name, 2, 100, "Name");
    return await store.Write(async db => { var u = await db.Users.SingleAsync(x => x.Id == user); u.Name = req.Name.Trim(); Store.Audit(db, user, "profile.updated", user); return Security.Profile(u); });
});
api.MapPost("/account/organizer", async (HttpContext ctx, Store store) =>
{
    var user = Security.Require(ctx);
    return await store.Write(async db => { var u = await db.Users.SingleAsync(x => x.Id == user); if (u.Role == "Customer") { u.Role = "Organizer"; Store.Audit(db, user, "organizer.onboarded", user); } return Security.Profile(u); });
});
api.MapPut("/account/password", async (PasswordRequest req, HttpContext ctx, Store store, PasswordHasher<User> hasher) =>
{
    var user = Security.Require(ctx);
    Security.CheckText(req.NewPassword, 10, 128, "New password");
    return await store.Write(async db =>
    {
        var u = await db.Users.SingleAsync(x => x.Id == user);
        if (req.CurrentPassword.Length > 128 || hasher.VerifyHashedPassword(u, u.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new ApiError(401, "INVALID_CREDENTIALS", "Current password is incorrect.");
        u.PasswordHash = hasher.HashPassword(u, req.NewPassword);
        var current = Security.Hash(ctx.Request.Cookies["eventhub_session"] ?? "");
        await db.Sessions.Where(x => x.UserId == user && x.Id != current).ExecuteDeleteAsync();
        Store.Audit(db, user, "password.changed", user);
        return new
        {
            ok = true
        };
    });
});
api.MapGet("/events", async (HttpContext ctx, CatalogCache cache) =>
{
    var items = await cache.Events();
    var q = ctx.Request.Query;
    var filtered = items.Where(x => x.Event.Status == "PUBLISHED" && x.Event.StartsAt > DateTime.UtcNow);
    if (q.TryGetValue("q", out var search) && !string.IsNullOrWhiteSpace(search))
        filtered = filtered.Where(x => (x.Event.Title + " " + x.Event.Artist + " " + x.Venue.Name).Contains(search.ToString(), StringComparison.OrdinalIgnoreCase));
    if (q.TryGetValue("category", out var category) && !string.IsNullOrEmpty(category) && category != "All")
        filtered = filtered.Where(x => x.Event.Category == category);
    if (q.TryGetValue("city", out var city) && city != "All cities" && !string.IsNullOrEmpty(city))
        filtered = filtered.Where(x => x.Venue.City == city);
    if (DateTime.TryParse(q["from"], out var from))
        filtered = filtered.Where(x => x.Event.StartsAt >= from.ToUniversalTime());
    if (DateTime.TryParse(q["to"], out var to))
        filtered = filtered.Where(x => x.Event.StartsAt < to.ToUniversalTime().AddDays(1));
    if (int.TryParse(q["maxPrice"], out var max))
        filtered = filtered.Where(x => x.Event.MinPrice <= max);
    filtered = q["sort"] == "price" ? filtered.OrderBy(x => x.Event.MinPrice) : filtered.OrderBy(x => x.Event.StartsAt);
    var all = filtered.ToList();
    var page = int.TryParse(q["page"], out var p) ? Math.Max(1, p) : 1;
    return new
    {
        items = all.Skip((page - 1) * 12).Take(12).Select(CatalogItem.View),
        total = all.Count,
        page,
        pageSize = 12
    };
});
api.MapGet("/events/{id:int}", async (int id, AppDb db) =>
{
    var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status == "PUBLISHED") ?? throw new ApiError(404, "EVENT_NOT_FOUND", "Event not found.");
    var venue = await db.Venues.AsNoTracking().SingleAsync(x => x.Id == ev.VenueId);
    var seats = await db.Seats.AsNoTracking().Where(x => x.EventId == id).ToListAsync();
    return new
    {
        ev,
        venue,
        capacity = seats.Count,
        available = seats.Count(x => x.Status == "AVAILABLE" || x.Status == "HELD" && x.ExpiresAt <= DateTime.UtcNow)
    };
});
api.MapGet("/events/{id:int}/seats", async (int id, AppDb db) =>
{
    if (!await db.Events.AnyAsync(x => x.Id == id && x.Status == "PUBLISHED"))
        throw new ApiError(404, "EVENT_NOT_FOUND", "Event not found.");
    var seats = await db.Seats.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync();
    return new
    {
        serverTime = DateTime.UtcNow,
        seats = seats.Select(x => BookingService.AvailabilityDto(x))
    };
});
api.MapGet("/venues", async (AppDb db) => await db.Venues.AsNoTracking().OrderBy(x => x.Name).ToListAsync());
api.MapPost("/holds", async (HoldRequest req, HttpContext ctx, BookingService service) => Results.Json(await service.Hold(Security.Require(ctx), Security.Key(ctx), req), statusCode: 201));
api.MapGet("/holds/{id}", async (string id, HttpContext ctx, BookingService service) => await service.GetHold(Security.Require(ctx), id));
api.MapDelete("/holds/{id}", async (string id, HttpContext ctx, BookingService service) => new { released = await service.Release(Security.Require(ctx), id) });
api.MapPost("/orders", async (OrderRequest req, HttpContext ctx, BookingService service) => Results.Json(await service.CreateOrder(Security.Require(ctx), Security.Key(ctx), req), statusCode: 201));
api.MapGet("/orders", async (HttpContext ctx, AppDb db) =>
{
    var user = Security.Require(ctx);
    return await (from o in db.Orders.AsNoTracking() join e in db.Events.AsNoTracking() on o.EventId equals e.Id where o.UserId == user orderby o.CreatedAt descending select new { order = o, ev = e }).Take(100).ToListAsync();
});
api.MapGet("/orders/{id}", async (string id, HttpContext ctx, PaymentService service) => await service.View(Security.Require(ctx), id));
api.MapPost("/orders/{id}/reconcile", async (string id, HttpContext ctx, AppDb db, PaymentService service) =>
{
    var user = Security.Require(ctx);
    if (!await db.Orders.AnyAsync(x => x.Id == id && x.UserId == user))
        throw new ApiError(404, "ORDER_NOT_FOUND", "Order not found.");
    await service.Reconcile(id);
    return await service.View(user, id);
});
api.MapPost("/payments/{id}/authorize", async (string id, PayRequest req, HttpContext ctx, PaymentService service) => await service.Start(Security.Require(ctx), id, Security.Key(ctx), req));
api.MapPost("/orders/{id}/refund", async (string id, ReasonRequest req, HttpContext ctx, PaymentService service) => await service.RequestRefund(Security.Require(ctx), id, req.Reason));
api.MapGet("/orders/{id}/invoice", async (string id, HttpContext ctx, AppDb db) =>
{
    var user = Security.Require(ctx);
    var o = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == user) ?? throw new ApiError(404, "ORDER_NOT_FOUND", "Order not found.");
    var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == o.EventId);
    var u = await db.Users.AsNoTracking().SingleAsync(x => x.Id == user);
    Func<string?, string?> encode = System.Net.WebUtility.HtmlEncode;
    string Money(int value) => $"{value / 100m:N2} {o.Currency}";
    return Results.Content($"<!doctype html><html lang='en'><meta charset='utf-8'><title>EventHub invoice</title><style>body{{font:16px system-ui;max-width:760px;margin:70px auto;padding:24px;color:#172222}}h1{{font-size:42px}}hr{{border:0;border-top:1px solid #ddd;margin:32px 0}}p{{line-height:1.8}}td{{padding:12px 24px 12px 0}}@media print{{body{{margin:0}}}}</style><h1>eventhub.</h1><h2>Booking receipt</h2><p>Order {encode(o.Id)}<br>{o.CreatedAt:dd MMM yyyy HH:mm} UTC<br>Status: {encode(o.Status)}</p><hr><h3>{encode(ev.Title)}</h3><p>{encode(u.Name)}<br>{encode(u.Email)}</p><table><tr><td>Tickets</td><td>{Money(o.Subtotal)}</td></tr><tr><td>Service fee (5%)</td><td>{Money(o.Fee)}</td></tr><tr><td><b>Total</b></td><td><b>{Money(o.Total)}</b></td></tr></table><hr><p>Payment provider: {encode(builder.Configuration["Payments:Provider"])}<br>Use your browser’s Print command to save this receipt as PDF.</p></html>", "text/html");
});
api.MapPost("/events/{id:int}/queue", async (int id, HttpContext ctx, BookingService service) => await service.JoinQueue(Security.Require(ctx), id));
api.MapGet("/events/{id:int}/queue", async (int id, HttpContext ctx, AppDb db) =>
{
    var user = Security.Require(ctx);
    var entry = await db.Queue.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id && x.UserId == user) ?? throw new ApiError(404, "QUEUE_NOT_JOINED", "Join the waiting room first.");
    var expired = entry.Status == "ADMITTED" && entry.AdmittedUntil <= DateTime.UtcNow;
    var position = await db.Queue.CountAsync(x => x.EventId == id && x.Status == "WAITING" && (x.CreatedAt < entry.CreatedAt || x.CreatedAt == entry.CreatedAt && x.Id < entry.Id));
    return new
    {
        status = expired ? "EXPIRED" : entry.Status,
        position = entry.Status == "ADMITTED" ? 0 : position + 1,
        admissionToken = entry.Status == "ADMITTED" && !expired ? entry.Token : null,
        entry.AdmittedUntil
    };
});
api.MapGet("/tickets/me", async (HttpContext ctx, AppDb db, TicketSigner signer) =>
{
    var user = Security.Require(ctx);
    var tickets = await db.Tickets.AsNoTracking().Where(x => x.UserId == user).ToListAsync();
    var ids = tickets.Select(x => x.EventId).Distinct().ToArray();
    var events = await db.Events.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();
    var venues = await db.Venues.AsNoTracking().ToListAsync();
    var seats = await db.Seats.AsNoTracking().Where(x => ids.Contains(x.EventId)).ToListAsync();
    return tickets.Select(t => new { ticket = t, ev = events.Single(e => e.Id == t.EventId), venue = venues.Single(v => v.Id == events.Single(e => e.Id == t.EventId).VenueId), seat = BookingService.SeatDto(seats.Single(s => s.EventId == t.EventId && s.Id == t.SeatId)), token = t.IsActive ? signer.Token(t) : null });
});
api.MapGet("/tickets/{id}/qr", async (string id, HttpContext ctx, AppDb db, TicketSigner signer) =>
{
    var user = Security.Require(ctx);
    var t = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == user) ?? throw new ApiError(404, "TICKET_NOT_FOUND", "Ticket not found.");
    if (!t.IsActive)
        throw new ApiError(410, "TICKET_REVOKED", "This ticket is no longer valid.");
    using var qr = QRCodeGenerator.GenerateQrCode(signer.Token(t), QRCodeGenerator.ECCLevel.Q);
    return Results.Content(new SvgQRCode(qr).GetGraphic(5), "image/svg+xml");
});
api.MapGet("/tickets/{id}/calendar", async (string id, HttpContext ctx, AppDb db) =>
{
    var user = Security.Require(ctx);
    var t = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == user) ?? throw new ApiError(404, "TICKET_NOT_FOUND", "Ticket not found.");
    var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == t.EventId);
    var v = await db.Venues.AsNoTracking().SingleAsync(x => x.Id == ev.VenueId);
    string Esc(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
    var ics = $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//EventHub//Tickets//EN\r\nBEGIN:VEVENT\r\nUID:{t.Id}@eventhub\r\nDTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\nDTSTART:{ev.StartsAt:yyyyMMddTHHmmssZ}\r\nDTEND:{ev.EndsAt:yyyyMMddTHHmmssZ}\r\nSUMMARY:{Esc(ev.Title)}\r\nLOCATION:{Esc(v.Name + ", " + v.Address)}\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
    return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar", "eventhub-event.ics");
});
api.MapPost("/tickets/{id}/transfer", async (string id, TransferRequest req, HttpContext ctx, Store store) =>
{
    var user = Security.Require(ctx);
    var email = ValidEmail(req.Email);
    return await store.Idempotent(user, "transfer", Security.Key(ctx), new
    {
        id,
        email
    }, async db =>
    {
        var t = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id && x.UserId == user) ?? throw new ApiError(404, "TICKET_NOT_FOUND", "Ticket not found.");
        var ev = await db.Events.SingleAsync(x => x.Id == t.EventId);
        var order = await db.Orders.SingleAsync(x => x.Id == t.OrderId);
        if (t.Status != "VALID" || t.TransferCount > 0 || ev.StartsAt <= DateTime.UtcNow || order.Status != "CONFIRMED")
            throw new ApiError(409, "TRANSFER_NOT_ALLOWED", "This ticket cannot be transferred.");
        var recipient = await db.Users.SingleOrDefaultAsync(x => x.Email == email) ?? throw new ApiError(404, "RECIPIENT_NOT_FOUND", "Ask the recipient to create an EventHub account first.");
        if (recipient.Id == user)
            throw new ApiError(400, "INVALID_RECIPIENT", "Choose a different account.");
        var recipientHolds = await db.Holds.Where(x => x.UserId == recipient.Id && x.EventId == t.EventId && (x.Status == "ALLOCATED" || x.Status == "ACTIVE" && x.ExpiresAt > DateTime.UtcNow)).Select(x => x.Id).ToListAsync();
        var reserved = await db.HoldSeats.CountAsync(x => recipientHolds.Contains(x.HoldId));
        if (reserved + await db.Tickets.CountAsync(x => x.UserId == recipient.Id && x.EventId == t.EventId && x.IsActive) >= 8)
            throw new ApiError(409, "BOOKING_LIMIT", "The recipient already has the maximum number of tickets.");
        t.UserId = recipient.Id;
        t.TokenVersion++;
        t.TransferCount++;
        Store.Audit(db, user, "ticket.transferred", id, email);
        Store.Notify(db, recipient.Id, "A ticket for you", $"A ticket for {ev.Title} was transferred to your account.");
        return new
        {
            ok = true
        };
    });
});
api.MapPost("/tickets/check-in", async (CheckInRequest req, HttpContext ctx, Store store, TicketSigner signer) =>
{
    var actor = Security.Require(ctx, "Staff", "Organizer", "Admin");
    Security.CheckText(req.Token, 20, 256, "Ticket code");
    Security.CheckText(req.Gate, 1, 60, "Gate");
    var id = req.Token.Split('.')[0];
    return await store.Write(async db =>
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiError(404, "INVALID_TICKET", "Ticket code is invalid.");
        var ev = await db.Events.SingleAsync(x => x.Id == ticket.EventId);
        var order = await db.Orders.SingleAsync(x => x.Id == ticket.OrderId);
        if (ctx.User.IsInRole("Organizer") && ev.OrganizerId != actor)
            throw new ApiError(403, "FORBIDDEN", "This event belongs to another organizer.");
        if (req.EventId != ticket.EventId)
            throw new ApiError(409, "WRONG_EVENT", "This ticket is for a different event.");
        if (!signer.Valid(req.Token, ticket) || !ticket.IsActive || ev.Status != "PUBLISHED" || order.Status != "CONFIRMED")
            throw new ApiError(409, "INVALID_TICKET", "This ticket is revoked, transferred, or awaiting a refund.");
        if (ticket.Status != "VALID")
            throw new ApiError(409, "ALREADY_CHECKED_IN", "This ticket has already been checked in.", new
            {
                ticket.CheckedInAt,
                ticket.Gate
            });
        if (ev.StartsAt > DateTime.UtcNow.AddHours(2) || ev.EndsAt < DateTime.UtcNow)
            throw new ApiError(409, "CHECKIN_NOT_OPEN", "Check-in opens 2 hours before the event and closes when it ends.");
        ticket.Status = "CHECKED_IN";
        ticket.CheckedInAt = DateTime.UtcNow;
        ticket.Gate = req.Gate;
        db.Scans.Add(new()
        {
            TicketId = ticket.Id,
            StaffId = actor,
            Gate = req.Gate
        });
        Store.Audit(db, actor, "ticket.checked_in", ticket.Id, req.Gate);
        var seat = await db.Seats.SingleAsync(x => x.EventId == ticket.EventId && x.Id == ticket.SeatId);
        var owner = await db.Users.SingleAsync(x => x.Id == ticket.UserId);
        return new
        {
            ok = true,
            attendee = owner.Name,
            seat = $"{seat.Section} · {seat.Row}{seat.Number}",
            eventTitle = ev.Title,
            ticket.CheckedInAt
        };
    });
});
api.MapGet("/favorites", async (HttpContext ctx, AppDb db) => { var user = Security.Require(ctx); return await db.Favorites.AsNoTracking().Where(x => x.UserId == user).Select(x => x.EventId).ToListAsync(); });
api.MapPut("/favorites/{id:int}", async (int id, HttpContext ctx, Store store) =>
{
    var user = Security.Require(ctx);
    return await store.Write(async db => { if (!await db.Events.AnyAsync(x => x.Id == id && x.Status == "PUBLISHED")) throw new ApiError(404, "EVENT_NOT_FOUND", "Event not found."); if (!await db.Favorites.AnyAsync(x => x.UserId == user && x.EventId == id)) db.Favorites.Add(new() { UserId = user, EventId = id }); return new { saved = true }; });
});
api.MapDelete("/favorites/{id:int}", async (int id, HttpContext ctx, Store store) => { var user = Security.Require(ctx); return await store.Write(async db => { await db.Favorites.Where(x => x.UserId == user && x.EventId == id).ExecuteDeleteAsync(); return new { saved = false }; }); });
api.MapGet("/notifications", async (HttpContext ctx, AppDb db) => { var user = Security.Require(ctx); return await db.Notifications.AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(); });
api.MapPut("/notifications/read", async (HttpContext ctx, Store store) => { var user = Security.Require(ctx); return await store.Write(async db => { await db.Notifications.Where(x => x.UserId == user).ExecuteUpdateAsync(x => x.SetProperty(n => n.Read, true)); return new { ok = true }; }); });

Management.Map(api);
api.MapPost("/webhooks/payments", async (HttpContext ctx, Store store, AppDb read, PaymentService payments) =>
{
    if (ctx.Request.ContentLength > 65536)
        throw new ApiError(413, "PAYLOAD_TOO_LARGE", "Webhook payload too large.");
    using var reader = new StreamReader(ctx.Request.Body);
    var raw = await reader.ReadToEndAsync();
    if (raw.Length > 65536)
        throw new ApiError(413, "PAYLOAD_TOO_LARGE", "Webhook payload too large.");
    var secret = builder.Configuration["Payments:StripeWebhookSecret"] ?? throw new ApiError(503, "WEBHOOK_NOT_CONFIGURED", "Payment webhook is not configured.");
    var parts = ctx.Request.Headers["Stripe-Signature"].ToString().Split(',').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToList();
    var time = parts.FirstOrDefault(x => x[0] == "t")?[1];
    if (!long.TryParse(time, out var unix) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix) > 300)
        throw new ApiError(400, "INVALID_SIGNATURE", "Webhook signature is invalid or expired.");
    var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(time + "." + raw))).ToLowerInvariant();
    if (!parts.Where(x => x[0] == "v1").Any(x => x[1].Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(x[1]), Encoding.UTF8.GetBytes(expected))))
        throw new ApiError(400, "INVALID_SIGNATURE", "Webhook signature is invalid.");
    var doc = JsonDocument.Parse(raw).RootElement;
    var eventId = doc.GetProperty("id").GetString()!;
    var paymentObject = doc.GetProperty("data").GetProperty("object");
    var reference = paymentObject.GetProperty("id").GetString();
    var attempt = await read.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Reference == reference);
    var hash = Security.Hash(raw);
    var duplicate = await store.Write(async db => { var prior = await db.Inbox.FindAsync(eventId); if (prior != null) { if (prior.Hash != hash) throw new ApiError(409, "WEBHOOK_MISMATCH", "This event ID has a different payload."); return true; } db.Inbox.Add(new() { Id = eventId, Hash = hash }); if (attempt != null) Store.Emit(db, "payment.reconcile", attempt.OrderId, new { orderId = attempt.OrderId }); return false; });
    // Provider lookup is authoritative; webhook payloads never directly mark a booking paid.
    if (attempt != null)
        await payments.Reconcile(attempt.OrderId);
    return new
    {
        received = true,
        duplicate
    };
});
app.Run();

async Task SignIn(HttpContext ctx, Store store, User user)
{
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var hash = Security.Hash(token);
    var expires = DateTime.UtcNow.AddDays(7);
    await store.Write(async db => { await db.Sessions.Where(x => x.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync(); db.Sessions.Add(new() { Id = hash, UserId = user.Id, ExpiresAt = expires }); return true; });
    ctx.Response.Cookies.Append("eventhub_session", token, new CookieOptions { HttpOnly = true, Secure = !app.Environment.IsDevelopment() || ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, Expires = expires, Path = "/" });
}
static string ValidEmail(string value)
{
    if (value.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(value, out var address) || address.Address != value || !value.Contains('.'))
        throw new ApiError(400, "VALIDATION_ERROR", "Enter a valid email address.");
    return value.Trim().ToLowerInvariant();
}
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ProfileRequest(string Name);
public record PasswordRequest(string CurrentPassword, string NewPassword);
public record ReasonRequest(string Reason);
public record TransferRequest(string Email);
public record CheckInRequest(string Token, int EventId, string Gate);
public partial class Program
{
}

public record CatalogItem(Event Event, Venue Venue)
{
    public static object View(CatalogItem x) => new { ev = x.Event, venue = x.Venue };
}
public class CatalogCache(IDbContextFactory<AppDb> factory, IConfiguration config, ILogger<CatalogCache> log)
{
    ConnectionMultiplexer? redis;
    public async Task<List<CatalogItem>> Events()
    {
        try
        {
            if (!string.IsNullOrEmpty(config["Redis:Connection"]))
            {
                redis ??= await ConnectionMultiplexer.ConnectAsync(config["Redis:Connection"]!);
                var cached = await redis.GetDatabase().StringGetAsync("eventhub:catalog:v1");
                if (cached.HasValue)
                    return JsonSerializer.Deserialize<List<CatalogItem>>(cached.ToString(), EventHub.Json.Options)!;
            }
        }
        catch (Exception ex) { log.LogWarning("Redis unavailable: {Kind}", ex.GetType().Name); }
        await using var db = await factory.CreateDbContextAsync();
        var items = await (from e in db.Events.AsNoTracking() join v in db.Venues.AsNoTracking() on e.VenueId equals v.Id select new CatalogItem(e, v)).ToListAsync();
        try
        {
            if (redis?.IsConnected == true)
                await redis.GetDatabase().StringSetAsync("eventhub:catalog:v1", EventHub.Json.Encode(items), TimeSpan.FromSeconds(30));
        }
        catch (Exception ex) { log.LogWarning("Catalog cache write failed: {Kind}", ex.GetType().Name); }
        return items;
    }
    public async Task Invalidate()
    {
        try
        {
            if (redis?.IsConnected == true)
                await redis.GetDatabase().KeyDeleteAsync("eventhub:catalog:v1");
        }
        catch (Exception ex) { log.LogWarning("Catalog invalidation failed: {Kind}", ex.GetType().Name); }
    }
}
