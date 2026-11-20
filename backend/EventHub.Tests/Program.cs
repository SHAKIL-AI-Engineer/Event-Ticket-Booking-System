using EventHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

await Check("late failure cannot overwrite a captured order awaiting refund",async f=>{
    f.Provider.OnGet=async()=>{
        await f.Store.Write(async db=>{
            var o=await db.Orders.SingleAsync();o.Status="REFUND_REQUESTED";
            var a=await db.Payments.SingleAsync();a.Status="CAPTURED";
            var h=await db.Holds.SingleAsync();h.Status="CONVERTED";
            var s=await db.Seats.SingleAsync();s.Status="SOLD";
            db.Tickets.Add(new(){OrderId=o.Id,UserId=o.UserId,EventId=o.EventId,SeatId=s.Id});return true;
        });
        return new("FAILED","existing",Error:"stale decline response");
    };
    await f.Service.Reconcile("order");
    await using var db=f.Factory.CreateDbContext();
    Require((await db.Orders.SingleAsync()).Status=="REFUND_REQUESTED");
    Require((await db.Payments.SingleAsync()).Status=="CAPTURED");
    Require((await db.Seats.SingleAsync()).Status=="SOLD");
});
await Check("late capture cannot resurrect a refunded booking",async f=>{
    f.Provider.OnGet=async()=>{
        await f.Store.Write(async db=>{
            (await db.Orders.SingleAsync()).Status="REFUNDED";
            (await db.Payments.SingleAsync()).Status="REFUNDED";
            (await db.Holds.SingleAsync()).Status="CONVERTED";
            var s=await db.Seats.SingleAsync();s.Status="AVAILABLE";s.HoldId=null;return true;
        });
        return new("CAPTURED","existing");
    };
    await f.Service.Reconcile("order");
    await using var db=f.Factory.CreateDbContext();
    Require((await db.Orders.SingleAsync()).Status=="REFUNDED");
    Require((await db.Payments.SingleAsync()).Status=="REFUNDED");
    Require(!await db.Tickets.AnyAsync());
    Require((await db.Seats.SingleAsync()).Status=="AVAILABLE");
});
await Check("nonfinal capture retains ownership for reconciliation",async f=>{
    await f.Store.Write(async db=>{
        (await db.Payments.SingleAsync()).Status="CREATED";
        var h=await db.Holds.SingleAsync();h.Status="ACTIVE";
        var s=await db.Seats.SingleAsync();s.Status="HELD";s.ExpiresAt=h.ExpiresAt;return true;
    });
    f.Provider.OnCapture=()=>Task.FromResult(new PaymentResult("AUTHORIZED","existing"));
    await f.Service.Reconcile("order");
    await using var db=f.Factory.CreateDbContext();
    Require((await db.Orders.SingleAsync()).Status=="PROCESSING");
    Require((await db.Payments.SingleAsync()).Status=="UNKNOWN_CAPTURE");
    Require((await db.Seats.SingleAsync()).Status=="ALLOCATED");
    Require(!await db.Tickets.AnyAsync());
});
Console.WriteLine("3 payment race regression checks passed.");

static void Require(bool value){if(!value)throw new Exception("Payment invariant failed.");}
static async Task Check(string name,Func<Fixture,Task> test){await using var f=await Fixture.Create();await test(f);Console.WriteLine("PASS "+name);}
class Provider : IPaymentProvider {
    public string Name=>"Regression";
    public Func<Task<PaymentResult>> OnGet=()=>Task.FromResult(new PaymentResult("AUTHORIZED","existing"));
    public Func<Task<PaymentResult>> OnCapture=()=>Task.FromResult(new PaymentResult("CAPTURED","existing"));
    public Task<PaymentResult> Authorize(PaymentAttempt a,Order o)=>Task.FromResult(new PaymentResult("AUTHORIZED","existing"));
    public Task<PaymentResult> Capture(PaymentAttempt a)=>OnCapture();
    public Task<PaymentResult> Get(PaymentAttempt a)=>OnGet();
    public Task Void(PaymentAttempt a)=>Task.CompletedTask;
    public Task<string> Refund(PaymentAttempt a,string id,int amount)=>Task.FromResult(id);
}
class Fixture : IAsyncDisposable {
    public string Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"eventhub-race-"+Guid.NewGuid().ToString("N")+".db");
    public PlatformDbFactory Factory=null!;public Store Store=null!;public Provider Provider=new();public PaymentService Service=null!;
    public static async Task<Fixture> Create(){
        var f=new Fixture();f.Factory=new(new DbContextOptionsBuilder<AppDb>().UseSqlite("Data Source="+f.Path).Options);
        f.Store=new(f.Factory);f.Service=new(f.Store,f.Factory,f.Provider,NullLogger<PaymentService>.Instance);
        await using var db=f.Factory.CreateDbContext();await db.Database.EnsureCreatedAsync();
        db.Users.Add(new(){Id="user",Name="Race fixture",Email="race@test.invalid",PasswordHash="unused"});
        db.Venues.Add(new(){Id=1,Name="Fixture",City="Dhaka",Address="Fixture avenue"});
        db.Events.Add(new(){Id=1,VenueId=1,OrganizerId="user",Title="Fixture",Slug="fixture",StartsAt=DateTime.UtcNow.AddDays(2),EndsAt=DateTime.UtcNow.AddDays(2).AddHours(2)});
        db.Holds.Add(new(){Id="hold",UserId="user",EventId=1,Status="ALLOCATED",ExpiresAt=DateTime.UtcNow.AddMinutes(5),Subtotal=10000,Fee=500,Total=10500});
        db.Seats.Add(new(){EventId=1,Id=1,Row="A",Number=1,Section="Standard",Price=10000,Status="ALLOCATED",HoldId="hold"});
        db.HoldSeats.Add(new(){HoldId="hold",EventId=1,SeatId=1,Price=10000});
        db.Orders.Add(new(){Id="order",HoldId="hold",UserId="user",EventId=1,Status="PROCESSING",Subtotal=10000,Fee=500,Total=10500});
        db.Payments.Add(new(){Id="payment",OrderId="order",Status="ALLOCATED",Reference="existing",Provider="Regression"});
        await db.SaveChangesAsync();return f;
    }
    public ValueTask DisposeAsync(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();System.IO.File.Delete(Path);return ValueTask.CompletedTask;}
}
