using Microsoft.EntityFrameworkCore;

namespace EventHub;

public record EventRequest(string Title, string Artist, string Category, string Description, int VenueId, DateTime StartsAt, DateTime EndsAt, int MinPrice, string Image, bool QueueEnabled, bool Featured, string Status = "DRAFT");
public record VenueRequest(string Name, string City, string Address, string TimeZone, int Rows, int SeatsPerRow);
public record PriceRequest(string Section, int Price);
public record RoleRequest(string Role);
public record RefundReview(bool Approve);
public static class Management
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/organizer/overview", async (HttpContext ctx, AppDb db) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            var admin = ctx.User.IsInRole("Admin");
            var events = await db.Events.AsNoTracking().Where(x => admin || x.OrganizerId == user).ToListAsync();
            var ids = events.Select(x => x.Id).ToArray();
            var orders = await db.Orders.AsNoTracking().Where(x => ids.Contains(x.EventId)).ToListAsync();
            var seats = await db.Seats.AsNoTracking().Where(x => ids.Contains(x.EventId)).ToListAsync();
            var tickets = await db.Tickets.AsNoTracking().Where(x => ids.Contains(x.EventId)).ToListAsync();
            var confirmed = orders.Where(x => x.Status is "CONFIRMED" or "REFUND_REQUESTED").ToList();
            var chart = Enumerable.Range(0, 7).Select(i => DateTime.UtcNow.Date.AddDays(i - 6)).Select(day => new { date = day.ToString("yyyy-MM-dd"), revenue = confirmed.Where(x => x.CreatedAt.Date == day).Sum(x => (long)x.Total), tickets = tickets.Count(x => orders.Any(o => o.Id == x.OrderId && o.CreatedAt.Date == day) && x.IsActive) });
            return new
            {
                revenue = confirmed.Sum(x => (long)x.Total),
                ticketsSold = tickets.Count(x => x.IsActive),
                totalEvents = events.Count,
                occupancy = seats.Count == 0 ? 0 : Math.Round(100m * seats.Count(x => x.Status == "SOLD") / seats.Count, 1),
                checkedIn = tickets.Count(x => x.Status == "CHECKED_IN"),
                chart,
                recentOrders = orders.OrderByDescending(x => x.CreatedAt).Take(8).Select(o => new { order = o, eventTitle = events.Single(x => x.Id == o.EventId).Title })
            };
        });
        api.MapGet("/organizer/events", async (HttpContext ctx, AppDb db) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            var admin = ctx.User.IsInRole("Admin");
            var events = await db.Events.AsNoTracking().Where(x => admin || x.OrganizerId == user).OrderByDescending(x => x.Id).ToListAsync();
            var ids = events.Select(x => x.Id).ToArray();
            var seats = await db.Seats.AsNoTracking().Where(x => ids.Contains(x.EventId)).ToListAsync();
            var venues = await db.Venues.AsNoTracking().ToListAsync();
            return events.Select(e => new { ev = e, venue = venues.Single(x => x.Id == e.VenueId), capacity = seats.Count(x => x.EventId == e.Id), sold = seats.Count(x => x.EventId == e.Id && x.Status == "SOLD") });
        });
        api.MapPost("/organizer/events", async (EventRequest req, HttpContext ctx, Store store, CatalogCache cache) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            ValidateEvent(req);
            var result = await store.Idempotent(user, "event.create", Security.Key(ctx), req, async db =>
            {
                var venue = await db.Venues.SingleOrDefaultAsync(x => x.Id == req.VenueId) ?? throw new ApiError(404, "VENUE_NOT_FOUND", "Choose an existing venue.");
                var e = new Event { OrganizerId = user, Slug = Slug(req.Title) + "-" + Guid.NewGuid().ToString("N")[..6] };
                Apply(e, req);
                db.Events.Add(e);
                await db.SaveChangesAsync();
                Seed.CreateSeats(db, e, venue);
                Store.Audit(db, user, "event.created", e.Id.ToString());
                return e;
            });
            await cache.Invalidate();
            return Results.Json(result, statusCode: 201);
        });
        api.MapPut("/organizer/events/{id:int}", async (int id, EventRequest req, HttpContext ctx, Store store, CatalogCache cache) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            ValidateEvent(req);
            var result = await store.Write(async db =>
            {
                var e = await OwnedEvent(db, id, user, ctx.User.IsInRole("Admin"));
                if (e.Status == "CANCELLED")
                    throw new ApiError(409, "EVENT_CANCELLED", "Cancelled events cannot be edited.");
                var occupied = await db.Seats.AnyAsync(x => x.EventId == id && x.Status != "AVAILABLE");
                if (occupied && (e.VenueId != req.VenueId || e.MinPrice != req.MinPrice || req.Status == "DRAFT"))
                    throw new ApiError(409, "EVENT_HAS_BOOKINGS", "Venue, base price, and publication cannot change while seats are reserved or sold. Use section pricing for available seats.");
                if (e.VenueId != req.VenueId || e.MinPrice != req.MinPrice)
                {
                    var venue = await db.Venues.SingleOrDefaultAsync(x => x.Id == req.VenueId) ?? throw new ApiError(404, "VENUE_NOT_FOUND", "Venue not found.");
                    // Holds retain historical seat foreign keys; never destroy inventory after any booking history.
                    if (await db.HoldSeats.AnyAsync(x => x.EventId == id))
                        throw new ApiError(409, "INVENTORY_HAS_HISTORY", "Use section pricing; this event already has booking history.");
                    await db.Seats.Where(x => x.EventId == id).ExecuteDeleteAsync();
                    Apply(e, req);
                    Seed.CreateSeats(db, e, venue);
                }
                else
                    Apply(e, req);
                Store.Audit(db, user, "event.updated", id.ToString());
                return e;
            });
            await cache.Invalidate();
            return result;
        });
        api.MapPut("/organizer/events/{id:int}/prices", async (int id, PriceRequest req, HttpContext ctx, Store store, CatalogCache cache) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            if (req.Price < 100 || req.Price > 100000000)
                throw new ApiError(400, "INVALID_PRICE", "Price must be between ৳1 and ৳1,000,000.");
            var result = await store.Write(async db =>
            {
                var e = await OwnedEvent(db, id, user, ctx.User.IsInRole("Admin"));
                if (e.Status == "CANCELLED")
                    throw new ApiError(409, "EVENT_CANCELLED", "Cancelled events cannot be repriced.");
                var seats = await db.Seats.Where(x => x.EventId == id && x.Section == req.Section && x.Status == "AVAILABLE").ToListAsync();
                if (seats.Count == 0)
                    throw new ApiError(409, "NO_AVAILABLE_SEATS", "No available seats in this section.");
                foreach (var s in seats)
                {
                    s.Price = req.Price;
                    s.Version++;
                }
                await db.SaveChangesAsync();
                e.MinPrice = await db.Seats.Where(x => x.EventId == id).MinAsync(x => x.Price);
                Store.Emit(db, "seats.changed", id.ToString(), new
                {
                    eventId = id,
                    seats = seats.Select(BookingService.SeatDto)
                });
                Store.Audit(db, user, "pricing.updated", id.ToString(), $"{req.Section}: {req.Price}");
                return new
                {
                    updated = seats.Count
                };
            });
            await cache.Invalidate();
            return result;
        });
        api.MapPost("/organizer/events/{id:int}/cancel", async (int id, ReasonRequest req, HttpContext ctx, Store store, CatalogCache cache) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            Security.CheckText(req.Reason, 5, 500, "Reason");
            var result = await store.Write(async db =>
            {
                var e = await OwnedEvent(db, id, user, ctx.User.IsInRole("Admin"));
                if (e.Status == "CANCELLED")
                    return new
                    {
                        ok = true
                    };
                if (await db.Orders.AnyAsync(x => x.EventId == id && x.Status == "PROCESSING"))
                    throw new ApiError(409, "PAYMENTS_IN_PROGRESS", "Reconcile pending payments before cancelling this event.");
                e.Status = "CANCELLED";
                var holds = await db.Holds.Where(x => x.EventId == id && x.Status == "ACTIVE").ToListAsync();
                foreach (var h in holds)
                    h.Status = "RELEASED";
                var seats = await db.Seats.Where(x => x.EventId == id && x.Status == "HELD").ToListAsync();
                foreach (var s in seats)
                {
                    s.Status = "AVAILABLE";
                    s.HoldId = null;
                    s.ExpiresAt = null;
                    s.Version++;
                }
                var orders = await db.Orders.Where(x => x.EventId == id).ToListAsync();
                foreach (var o in orders)
                {
                    if (o.Status == "CONFIRMED")
                    {
                        o.Status = "REFUND_REQUESTED";
                        var refund = await db.Refunds.SingleOrDefaultAsync(x => x.OrderId == o.Id);
                        if (refund == null)
                            db.Refunds.Add(new()
                            {
                                OrderId = o.Id,
                                Amount = o.Total,
                                Reason = "Event cancelled: " + req.Reason
                            });
                        else if (refund.Status == "REJECTED")
                        {
                            refund.Status = "REQUESTED";
                            refund.Reason = "Event cancelled: " + req.Reason;
                        }
                        Store.Notify(db, o.UserId, "Event cancelled", $"{e.Title} has been cancelled. Your refund is awaiting review.");
                    }
                    else if (o.Status == "PENDING")
                        o.Status = "CANCELLED";
                }
                Store.Audit(db, user, "event.cancelled", id.ToString(), req.Reason);
                Store.Emit(db, "seats.changed", id.ToString(), new
                {
                    eventId = id,
                    seats = seats.Select(BookingService.SeatDto)
                });
                return new
                {
                    ok = true
                };
            });
            await cache.Invalidate();
            return result;
        });
        api.MapGet("/organizer/events/{id:int}/attendees", async (int id, HttpContext ctx, AppDb db) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin", "Staff");
            if (!ctx.User.IsInRole("Staff"))
                await OwnedEvent(db, id, user, ctx.User.IsInRole("Admin"));
            return await (from t in db.Tickets.AsNoTracking() join u in db.Users.AsNoTracking() on t.UserId equals u.Id join s in db.Seats.AsNoTracking() on new { t.EventId, Id = t.SeatId } equals new { s.EventId, s.Id } where t.EventId == id && t.IsActive orderby s.Id select new { t.Id, name = u.Name, u.Email, seat = s.Row + s.Number, section = s.Section, t.Status, t.CheckedInAt, t.Gate }).ToListAsync();
        });
        api.MapGet("/staff/events", async (HttpContext ctx, AppDb db) =>
        {
            var user = Security.Require(ctx, "Staff", "Organizer", "Admin");
            var scoped = ctx.User.IsInRole("Organizer");
            return await db.Events.AsNoTracking().Where(x => x.Status == "PUBLISHED" && (!scoped || x.OrganizerId == user)).OrderBy(x => x.StartsAt).ToListAsync();
        });
        api.MapPost("/organizer/venues", async (VenueRequest req, HttpContext ctx, Store store) =>
        {
            var user = Security.Require(ctx, "Organizer", "Admin");
            Security.CheckText(req.Name, 3, 120, "Venue name");
            Security.CheckText(req.City, 2, 80, "City");
            Security.CheckText(req.Address, 5, 300, "Address");
            if (req.Rows < 1 || req.Rows > 26 || req.SeatsPerRow < 1 || req.SeatsPerRow > 40)
                throw new ApiError(400, "INVALID_CAPACITY", "Venues support 1–26 rows and 1–40 seats per row.");
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(req.TimeZone);
            }
            catch { throw new ApiError(400, "INVALID_TIMEZONE", "Choose a valid IANA time zone."); }
            return await store.Idempotent(user, "venue.create", Security.Key(ctx), req, async db =>
            {
                var v = new Venue { Name = req.Name.Trim(), City = req.City.Trim(), Address = req.Address.Trim(), TimeZone = req.TimeZone, Rows = req.Rows, SeatsPerRow = req.SeatsPerRow };
                db.Venues.Add(v);
                Store.Audit(db, user, "venue.created", v.Name);
                await db.SaveChangesAsync();
                return (object)v;
            });
        });
        api.MapGet("/admin/overview", async (HttpContext ctx, AppDb db) =>
        {
            Security.Require(ctx, "Admin");
            var now = DateTime.UtcNow;
            var activeHolds = await db.Holds.CountAsync(x => x.Status == "ACTIVE" && x.ExpiresAt > now);
            var payments = await db.Payments.AsNoTracking().ToListAsync();
            var exceptions = payments.Where(x => x.Status is "UNKNOWN" or "UNKNOWN_CAPTURE" or "CAPTURED_UNALLOCATED" or "ALLOCATED" or "CREATED" or "AUTHORIZED").Select(x => new { x.Id, x.OrderId, x.Provider, x.Status, x.UpdatedAt, x.Error });
            return new
            {
                activeHolds,
                confirmedOrders = await db.Orders.CountAsync(x => x.Status == "CONFIRMED"),
                pendingPayments = exceptions.Count(),
                pendingRefunds = await db.Refunds.CountAsync(x => x.Status == "REQUESTED" || x.Status == "PROCESSING"),
                outboxPending = await db.Outbox.CountAsync(x => x.PublishedAt == null),
                users = await db.Users.CountAsync(),
                exceptions,
                refunds = await db.Refunds.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(),
                audit = await db.Audit.AsNoTracking().OrderByDescending(x => x.Id).Take(40).ToListAsync()
            };
        });
        api.MapGet("/admin/users", async (HttpContext ctx, AppDb db) => { Security.Require(ctx, "Admin"); return await db.Users.AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.Name, x.Email, x.Role, x.CreatedAt }).Take(500).ToListAsync(); });
        api.MapPut("/admin/users/{id}/role", async (string id, RoleRequest req, HttpContext ctx, Store store) =>
        {
            var actor = Security.Require(ctx, "Admin");
            if (req.Role is not ("Customer" or "Organizer" or "Staff" or "Admin"))
                throw new ApiError(400, "INVALID_ROLE", "Invalid role.");
            return await store.Write(async db => { if (id == actor) throw new ApiError(409, "SELF_ROLE_CHANGE", "Ask another administrator to change your role."); var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiError(404, "USER_NOT_FOUND", "User not found."); u.Role = req.Role; Store.Audit(db, actor, "role.changed", id, req.Role); return Security.Profile(u); });
        });
        api.MapPost("/admin/payments/{id}/reconcile", async (string id, HttpContext ctx, PaymentService service, AppDb db) =>
        {
            var actor = Security.Require(ctx, "Admin");
            var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == id) ?? throw new ApiError(404, "PAYMENT_NOT_FOUND", "Payment not found.");
            if (payment.Status == "CAPTURED_UNALLOCATED")
                throw new ApiError(409, "MANUAL_REFUND_REQUIRED", "Use the payment provider dashboard to refund the captured payment, then record the provider reference in the incident log. This exception cannot be safely fulfilled automatically.");
            await service.Reconcile(id);
            return await service.View(actor, id, true);
        });
        api.MapPost("/admin/refunds/{id}/review", async (string id, RefundReview req, HttpContext ctx, PaymentService service) => await service.ReviewRefund(Security.Require(ctx, "Admin"), id, req.Approve));
        api.MapGet("/admin/orders", async (HttpContext ctx, AppDb db) => { Security.Require(ctx, "Admin"); return await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(); });
    }
    static async Task<Event> OwnedEvent(AppDb db, int id, string user, bool admin) => await db.Events.SingleOrDefaultAsync(x => x.Id == id && (admin || x.OrganizerId == user)) ?? throw new ApiError(404, "EVENT_NOT_FOUND", "Event not found or belongs to another organizer.");
    static void ValidateEvent(EventRequest r)
    {
        Security.CheckText(r.Title, 3, 150, "Title");
        Security.CheckText(r.Artist, 2, 200, "Artist / host");
        Security.CheckText(r.Description, 20, 6000, "Description");
        if (!new[] { "Music", "Comedy", "Technology", "Arts & Theatre", "Food & Culture", "Sports" }.Contains(r.Category))
            throw new ApiError(400, "INVALID_CATEGORY", "Choose a valid category.");
        if (r.StartsAt.ToUniversalTime() <= DateTime.UtcNow || r.EndsAt.ToUniversalTime() <= r.StartsAt.ToUniversalTime())
            throw new ApiError(400, "INVALID_SCHEDULE", "Choose a future start and an end after the start.");
        if (r.MinPrice < 100 || r.MinPrice > 50000000)
            throw new ApiError(400, "INVALID_PRICE", "Base ticket price must be between ৳1 and ৳500,000.");
        if (r.Status is not ("DRAFT" or "PUBLISHED"))
            throw new ApiError(400, "INVALID_STATUS", "Choose draft or published.");
        if (!new[] { "/images/concert.jpg", "/images/festival.jpg", "/images/comedy.jpg", "/images/tech.jpg", "/images/theatre.jpg", "/images/food.jpg", "/images/sports.jpg", "/images/jazz.jpg" }.Contains(r.Image))
            throw new ApiError(400, "INVALID_IMAGE", "Choose one of the event cover images.");
    }
    static void Apply(Event e, EventRequest r)
    {
        e.Title = r.Title.Trim();
        e.Artist = r.Artist.Trim();
        e.Description = r.Description.Trim();
        e.Category = r.Category;
        e.VenueId = r.VenueId;
        e.StartsAt = r.StartsAt.ToUniversalTime();
        e.EndsAt = r.EndsAt.ToUniversalTime();
        e.MinPrice = r.MinPrice;
        e.Image = r.Image;
        e.QueueEnabled = r.QueueEnabled;
        e.Featured = r.Featured;
        e.Status = r.Status;
    }
    static string Slug(string s) => System.Text.RegularExpressions.Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}
