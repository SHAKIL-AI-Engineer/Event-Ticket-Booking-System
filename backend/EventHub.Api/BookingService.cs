using Prometheus;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EventHub;
public record HoldRequest(int EventId, int[] SeatIds, string? AdmissionToken);
public record OrderRequest(string HoldId);
public record SeatView(int Id,string Row,int Number,string Section,int Price,string Status,long Version);
public record HoldView(string Id,int EventId,string Status,DateTime ExpiresAt,DateTime ServerTime,int Subtotal,int Fee,int Total,List<SeatView> Seats);
public class BookingService(Store store,IDbContextFactory<AppDb> factory,IConfiguration config) {
    public static SeatView SeatDto(Seat x) => new(x.Id,x.Row,x.Number,x.Section,x.Price,x.Status,x.Version);
    public static async Task Expire(AppDb db) {
        var now=DateTime.UtcNow;
        var holds=await db.Holds.Where(x=>x.Status=="ACTIVE"&&x.ExpiresAt<=now).OrderBy(x=>x.Id).Take(200).ToListAsync();
        foreach(var h in holds) {
            var seats=await db.Seats.Where(x=>x.HoldId==h.Id&&x.Status=="HELD").OrderBy(x=>x.Id).ToListAsync();
            foreach(var s in seats){s.Status="AVAILABLE";s.HoldId=null;s.ExpiresAt=null;s.Version++;}
            h.Status="EXPIRED";
            var order=await db.Orders.SingleOrDefaultAsync(x=>x.HoldId==h.Id);
            if(order!=null&&order.Status=="PENDING") order.Status="EXPIRED";
            if(seats.Count>0) Store.Emit(db,"seats.changed",h.EventId.ToString(),new{eventId=h.EventId,seats=seats.Select(SeatDto)});
        }
    }
    public Task<JsonElement> Hold(string user,string key,HoldRequest req) {
        Telemetry.Holds.Inc();
        return store.Idempotent(user,"hold",key,req,async db=> {
            using var timer=Telemetry.HoldLatency.NewTimer();
            if(req.SeatIds==null||req.SeatIds.Length is <1 or >8||req.SeatIds.Distinct().Count()!=req.SeatIds.Length) throw new ApiError(400,"INVALID_SEATS","Choose between 1 and 8 different seats.");
            await Expire(db);
            var ev=await db.Events.SingleOrDefaultAsync(x=>x.Id==req.EventId)??throw new ApiError(404,"EVENT_NOT_FOUND","Event not found.");
            if(ev.Status!="PUBLISHED"||ev.StartsAt<=DateTime.UtcNow) throw new ApiError(409,"EVENT_CLOSED","Booking is closed for this event.");
            if(ev.QueueEnabled&&!await db.Queue.AnyAsync(x=>x.EventId==ev.Id&&x.UserId==user&&x.Token==req.AdmissionToken&&x.Status=="ADMITTED"&&x.AdmittedUntil>DateTime.UtcNow)) throw new ApiError(403,"ADMISSION_REQUIRED","Join the waiting room before reserving seats.");
            var owned=await db.Tickets.CountAsync(x=>x.UserId==user&&x.EventId==ev.Id&&x.IsActive);
            var activeHolds=await db.Holds.Where(x=>x.UserId==user&&x.EventId==ev.Id&&x.Status=="ACTIVE"&&x.ExpiresAt>DateTime.UtcNow).Select(x=>x.Id).ToListAsync();
            var heldCount=await db.HoldSeats.CountAsync(x=>activeHolds.Contains(x.HoldId));
            if(owned+heldCount+req.SeatIds.Length>8) throw new ApiError(409,"BOOKING_LIMIT","You can reserve or own at most 8 tickets per event.");
            var seats=await db.Seats.Where(x=>x.EventId==req.EventId&&req.SeatIds.Contains(x.Id)).OrderBy(x=>x.Id).ToListAsync();
            var unavailable=seats.Where(x=>x.Status!="AVAILABLE"&&!(x.Status=="HELD"&&x.ExpiresAt<=DateTime.UtcNow)).Select(x=>x.Id).ToArray();
            if(seats.Count!=req.SeatIds.Length||unavailable.Length>0) { Telemetry.Conflicts.Inc(); throw new ApiError(409,"SEAT_UNAVAILABLE","Some seats were just reserved. Please choose another seat.",new{unavailableSeatIds=unavailable}); }
            var subtotal=seats.Sum(x=>x.Price); var fee=(subtotal*5+50)/100;
            var h=new Hold{UserId=user,EventId=req.EventId,ExpiresAt=DateTime.UtcNow.AddSeconds(config.GetValue("Booking:HoldSeconds",300)),Subtotal=subtotal,Fee=fee,Total=subtotal+fee};
            db.Holds.Add(h);
            foreach(var s in seats){s.Status="HELD";s.HoldId=h.Id;s.ExpiresAt=h.ExpiresAt;s.Version++; db.HoldSeats.Add(new(){HoldId=h.Id,EventId=s.EventId,SeatId=s.Id,Price=s.Price});}
            Store.Emit(db,"seats.changed",ev.Id.ToString(),new{eventId=ev.Id,seats=seats.Select(SeatDto)});
            Store.Audit(db,user,"hold.created",h.Id);
            return new HoldView(h.Id,h.EventId,h.Status,h.ExpiresAt,DateTime.UtcNow,h.Subtotal,h.Fee,h.Total,seats.Select(SeatDto).ToList());
        });
    }
    public async Task<HoldView> GetHold(string user,string id) {
        await store.Write(async db=>{await Expire(db); return true;});
        await using var db=await factory.CreateDbContextAsync();
        var h=await db.Holds.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==user)??throw new ApiError(404,"HOLD_NOT_FOUND","Reservation not found.");
        var ids=await db.HoldSeats.Where(x=>x.HoldId==id).Select(x=>x.SeatId).ToListAsync();
        var seats=await db.Seats.Where(x=>x.EventId==h.EventId&&ids.Contains(x.Id)).OrderBy(x=>x.Id).ToListAsync();
        return new(h.Id,h.EventId,h.Status,h.ExpiresAt,DateTime.UtcNow,h.Subtotal,h.Fee,h.Total,seats.Select(SeatDto).ToList());
    }
    public Task<bool> Release(string user,string id) => store.Write(async db=> {
        var h=await db.Holds.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==user)??throw new ApiError(404,"HOLD_NOT_FOUND","Reservation not found.");
        if(h.Status!="ACTIVE") return true;
        if(await db.Payments.AnyAsync(x=>db.Orders.Any(o=>o.Id==x.OrderId&&o.HoldId==id)&&x.Status!="FAILED"&&x.Status!="VOIDED")) throw new ApiError(409,"PAYMENT_IN_PROGRESS","Payment is being verified. Please check your order status.");
        var seats=await db.Seats.Where(x=>x.HoldId==id&&x.Status=="HELD").ToListAsync();
        foreach(var s in seats){s.Status="AVAILABLE";s.HoldId=null;s.ExpiresAt=null;s.Version++;}
        h.Status="RELEASED";
        var order=await db.Orders.SingleOrDefaultAsync(x=>x.HoldId==id); if(order?.Status=="PENDING") order.Status="CANCELLED";
        Store.Emit(db,"seats.changed",h.EventId.ToString(),new{eventId=h.EventId,seats=seats.Select(SeatDto)}); return true;
    });
    public Task<JsonElement> CreateOrder(string user,string key,OrderRequest req) => store.Idempotent(user,"order",key,req,async db=> {
        var h=await db.Holds.SingleOrDefaultAsync(x=>x.Id==req.HoldId&&x.UserId==user)??throw new ApiError(404,"HOLD_NOT_FOUND","Reservation not found.");
        var previous=await db.Orders.SingleOrDefaultAsync(x=>x.HoldId==h.Id);
        if(previous!=null) return previous;
        if(h.Status!="ACTIVE"||h.ExpiresAt<=DateTime.UtcNow) throw new ApiError(410,"HOLD_EXPIRED","Your reservation expired. Please choose your seats again.");
        var ev=await db.Events.SingleAsync(x=>x.Id==h.EventId);
        var o=new Order{HoldId=h.Id,UserId=user,EventId=h.EventId,Subtotal=h.Subtotal,Fee=h.Fee,Total=h.Total,Currency=ev.Currency};
        db.Orders.Add(o); Store.Audit(db,user,"order.created",o.Id); return o;
    });
    public Task<object> JoinQueue(string user,int eventId) => store.Write<object>(async db=> {
        var ev=await db.Events.SingleOrDefaultAsync(x=>x.Id==eventId&&x.Status=="PUBLISHED")??throw new ApiError(404,"EVENT_NOT_FOUND","Event not found.");
        if(ev.StartsAt<=DateTime.UtcNow) throw new ApiError(409,"EVENT_CLOSED","Booking is closed.");
        var entry=await db.Queue.SingleOrDefaultAsync(x=>x.EventId==eventId&&x.UserId==user);
        if(entry==null){entry=new(){EventId=eventId,UserId=user};db.Queue.Add(entry);await db.SaveChangesAsync();}
        if(entry.Status=="EXPIRED"||entry.Status=="ADMITTED"&&entry.AdmittedUntil<=DateTime.UtcNow){entry.Status="WAITING";entry.CreatedAt=DateTime.UtcNow;entry.Token=Guid.NewGuid().ToString("N");}
        await db.SaveChangesAsync();
        await PromoteQueue(db,eventId);
        await db.SaveChangesAsync();
        var ahead=await db.Queue.CountAsync(x=>x.EventId==eventId&&x.Status=="WAITING"&&(x.CreatedAt<entry.CreatedAt||(x.CreatedAt==entry.CreatedAt&&x.Id<entry.Id)));
        return new{entry.Status,position=entry.Status=="ADMITTED"?0:ahead+1,admissionToken=entry.Status=="ADMITTED"?entry.Token:null,entry.AdmittedUntil,serverTime=DateTime.UtcNow};
    });
    public async Task PromoteQueue(AppDb db,int eventId) {
        var expired=await db.Queue.Where(x=>x.EventId==eventId&&x.Status=="ADMITTED"&&x.AdmittedUntil<=DateTime.UtcNow).ToListAsync();
        foreach(var x in expired)x.Status="EXPIRED";
        var admitted=await db.Queue.CountAsync(x=>x.EventId==eventId&&x.Status=="ADMITTED"&&x.AdmittedUntil>DateTime.UtcNow);
        var capacity=config.GetValue("Booking:AdmissionCapacity",100);
        var entries=await db.Queue.Where(x=>x.EventId==eventId&&x.Status=="WAITING").OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Take(Math.Max(0,capacity-admitted)).ToListAsync();
        foreach(var x in entries){x.Status="ADMITTED";x.AdmittedUntil=DateTime.UtcNow.AddSeconds(config.GetValue("Booking:AdmissionSeconds",600));}
    }
}
