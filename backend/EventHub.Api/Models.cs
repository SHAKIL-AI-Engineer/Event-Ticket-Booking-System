using Microsoft.EntityFrameworkCore;

namespace EventHub;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = ""; public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = ""; public string Role { get; set; } = "Customer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Session
{
    public string Id { get; set; } = ""; public string UserId { get; set; } = "";
    public DateTime ExpiresAt
    {
        get; set;
    }
}
public class Venue
{
    public int Id
    {
        get; set;
    }
    public string Name { get; set; } = ""; public string City { get; set; } = "";
    public string Address { get; set; } = ""; public string TimeZone { get; set; } = "Asia/Dhaka";
    public int Rows { get; set; } = 8; public int SeatsPerRow { get; set; } = 12;
}
public class Event
{
    public int Id
    {
        get; set;
    }
    public string Title { get; set; } = ""; public string Slug { get; set; } = "";
    public string Category { get; set; } = "Music"; public string Artist { get; set; } = "";
    public string Description { get; set; } = ""; public string Image { get; set; } = "";
    public int VenueId
    {
        get; set;
    }
    public string OrganizerId { get; set; } = "";
    public DateTime StartsAt
    {
        get; set;
    }
    public DateTime EndsAt
    {
        get; set;
    }
    public string Status { get; set; } = "DRAFT"; public bool Featured
    {
        get; set;
    }
    public bool QueueEnabled
    {
        get; set;
    }
    public int MinPrice
    {
        get; set;
    }
    public string Currency { get; set; } = "BDT"; public string Policies { get; set; } = "Tickets may be transferred once before check-in. Refunds are available up to 24 hours before the event. Bring a valid photo ID. Doors open 60 minutes before the event.";
}
public class Seat
{
    public int EventId
    {
        get; set;
    }
    public int Id
    {
        get; set;
    }
    public string Row { get; set; } = "";
    public int Number
    {
        get; set;
    }
    public string Section { get; set; } = ""; public int Price
    {
        get; set;
    }
    public string Status { get; set; } = "AVAILABLE"; public string? HoldId
    {
        get; set;
    }
    public DateTime? ExpiresAt
    {
        get; set;
    }
    public long Version
    {
        get; set;
    }
}
public class Hold
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string UserId { get; set; } = "";
    public int EventId
    {
        get; set;
    }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime ExpiresAt
    {
        get; set;
    }
    public int Subtotal
    {
        get; set;
    }
    public int Fee
    {
        get; set;
    }
    public int Total
    {
        get; set;
    }
}
public class HoldSeat
{
    public string HoldId { get; set; } = ""; public int EventId
    {
        get; set;
    }
    public int SeatId
    {
        get; set;
    }
    public int Price
    {
        get; set;
    }
}
public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string HoldId { get; set; } = "";
    public string UserId { get; set; } = ""; public int EventId
    {
        get; set;
    }
    public string Status { get; set; } = "PENDING"; public int Subtotal
    {
        get; set;
    }
    public int Fee
    {
        get; set;
    }
    public int Total
    {
        get; set;
    }
    public string Currency { get; set; } = "BDT"; public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RefundReason
    {
        get; set;
    }
}
public class PaymentAttempt
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string OrderId { get; set; } = "";
    public string Provider { get; set; } = ""; public string Reference { get; set; } = "";
    public string Status { get; set; } = "CREATED"; public string Outcome { get; set; } = "success";
    public string? ClientSecret
    {
        get; set;
    }
    public string? Error
    {
        get; set;
    }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class Ticket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string OrderId { get; set; } = "";
    public string UserId { get; set; } = ""; public int EventId
    {
        get; set;
    }
    public int SeatId
    {
        get; set;
    }
    public string Status { get; set; } = "VALID"; public bool IsActive { get; set; } = true;
    public int TokenVersion { get; set; } = 1; public int TransferCount
    {
        get; set;
    }
    public DateTime? CheckedInAt
    {
        get; set;
    }
    public string? Gate
    {
        get; set;
    }
}
public class TicketScan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string TicketId { get; set; } = "";
    public string StaffId { get; set; } = ""; public string Gate { get; set; } = ""; public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class IdempotencyRecord
{
    public int Id
    {
        get; set;
    }
    public string UserId { get; set; } = ""; public string Scope { get; set; } = "";
    public string Key { get; set; } = ""; public string Fingerprint { get; set; } = ""; public string Response { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class OutboxEvent
{
    public long Id
    {
        get; set;
    }
    public string Type { get; set; } = ""; public string AggregateId { get; set; } = "";
    public string Payload { get; set; } = ""; public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt
    {
        get; set;
    }
    public int Attempts
    {
        get; set;
    }
}
public class InboxEvent
{
    public string Id { get; set; } = ""; public string Hash { get; set; } = ""; public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class AuditLog
{
    public long Id
    {
        get; set;
    }
    public string ActorId { get; set; } = ""; public string Action { get; set; } = "";
    public string Target { get; set; } = ""; public string Detail { get; set; } = ""; public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Notification
{
    public long Id
    {
        get; set;
    }
    public string UserId { get; set; } = ""; public string Title { get; set; } = "";
    public string Body { get; set; } = ""; public bool Read
    {
        get; set;
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime? EmailedAt
    {
        get; set;
    }
}
public class Favorite
{
    public string UserId { get; set; } = ""; public int EventId
    {
        get; set;
    }
}
public class QueueEntry
{
    public long Id
    {
        get; set;
    }
    public string UserId { get; set; } = ""; public int EventId
    {
        get; set;
    }
    public string Token { get; set; } = Guid.NewGuid().ToString("N"); public string Status { get; set; } = "WAITING";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime? AdmittedUntil
    {
        get; set;
    }
}
public class Refund
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string OrderId { get; set; } = "";
    public int Amount
    {
        get; set;
    }
    public string Reason { get; set; } = ""; public string Status { get; set; } = "REQUESTED";
    public string? ProviderReference
    {
        get; set;
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>(); public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Venue> Venues => Set<Venue>(); public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>(); public DbSet<Hold> Holds => Set<Hold>(); public DbSet<HoldSeat> HoldSeats => Set<HoldSeat>();
    public DbSet<Order> Orders => Set<Order>(); public DbSet<PaymentAttempt> Payments => Set<PaymentAttempt>();
    public DbSet<Ticket> Tickets => Set<Ticket>(); public DbSet<TicketScan> Scans => Set<TicketScan>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>(); public DbSet<OutboxEvent> Outbox => Set<OutboxEvent>();
    public DbSet<InboxEvent> Inbox => Set<InboxEvent>(); public DbSet<AuditLog> Audit => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>(); public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<QueueEntry> Queue => Set<QueueEntry>(); public DbSet<Refund> Refunds => Set<Refund>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        foreach (var p in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
            p.SetMaxLength(p.Name is "Payload" or "Response" or "Description" or "Detail" or "Body" or "Policies" ? 8000 : 450);
        b.Entity<User>().Property(x => x.Id).HasMaxLength(32);
        b.Entity<User>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<IdempotencyRecord>().Property(x => x.UserId).HasMaxLength(32);
        b.Entity<IdempotencyRecord>().Property(x => x.Scope).HasMaxLength(80);
        b.Entity<IdempotencyRecord>().Property(x => x.Key).HasMaxLength(128);
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Session>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Event>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Event>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Event>().HasOne<User>().WithMany().HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Seat>().HasKey(x => new { x.EventId, x.Id });
        b.Entity<Seat>().HasIndex(x => new { x.Status, x.ExpiresAt });
        b.Entity<Seat>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Hold>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Hold>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Hold>().HasIndex(x => new { x.Status, x.ExpiresAt });
        b.Entity<HoldSeat>().HasKey(x => new { x.HoldId, x.SeatId });
        b.Entity<HoldSeat>().HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HoldSeat>().HasOne<Seat>().WithMany().HasForeignKey(x => new { x.EventId, x.SeatId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasIndex(x => x.HoldId).IsUnique();
        b.Entity<Order>().HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentAttempt>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<PaymentAttempt>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasIndex(x => new { x.EventId, x.SeatId }).IsUnique().HasFilter("[IsActive] = 1");
        b.Entity<Ticket>().HasOne<Seat>().WithMany().HasForeignKey(x => new { x.EventId, x.SeatId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketScan>().HasIndex(x => x.TicketId).IsUnique();
        b.Entity<IdempotencyRecord>().HasIndex(x => new { x.UserId, x.Scope, x.Key }).IsUnique();
        b.Entity<Favorite>().HasKey(x => new { x.UserId, x.EventId });
        b.Entity<QueueEntry>().HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
        b.Entity<Refund>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<OutboxEvent>().HasIndex(x => new { x.PublishedAt, x.Id });
        foreach (var entity in b.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.Name.EndsWith("Id")))
        {
            if (property.Name != "Id" || entity.ClrType != typeof(Session) && entity.ClrType != typeof(InboxEvent))
                property.SetMaxLength(32);
        }
        b.Entity<Refund>().Property(x => x.Reason).HasMaxLength(1000);
        b.Entity<Order>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Seat>().HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketScan>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketScan>().HasOne<User>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Refund>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Favorite>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Favorite>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<QueueEntry>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<QueueEntry>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venue>().ToTable(t => t.HasCheckConstraint("CK_Venue_Capacity", "[Rows] BETWEEN 1 AND 26 AND [SeatsPerRow] BETWEEN 1 AND 40"));
        b.Entity<Seat>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Seat_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Seat_State", "([Status] = 'AVAILABLE' AND [HoldId] IS NULL AND [ExpiresAt] IS NULL) OR ([Status] = 'HELD' AND [HoldId] IS NOT NULL AND [ExpiresAt] IS NOT NULL) OR ([Status] IN ('ALLOCATED', 'SOLD') AND [HoldId] IS NOT NULL AND [ExpiresAt] IS NULL)");
        });
        b.Entity<Hold>().ToTable(t => t.HasCheckConstraint("CK_Hold_Quote", "[Subtotal] > 0 AND [Fee] >= 0 AND [Total] = [Subtotal] + [Fee]"));
        b.Entity<Order>().ToTable(t => t.HasCheckConstraint("CK_Order_Quote", "[Subtotal] > 0 AND [Fee] >= 0 AND [Total] = [Subtotal] + [Fee]"));
        b.Entity<Ticket>().ToTable(t => t.HasCheckConstraint("CK_Ticket_State", "([IsActive] = 1 AND [Status] IN ('VALID', 'CHECKED_IN')) OR ([IsActive] = 0 AND [Status] = 'REFUNDED')"));

    }
}
