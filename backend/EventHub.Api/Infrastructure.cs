using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace EventHub;
public class ApiError(int status, string code, string message, object? details = null) : Exception(message) {
    public int Status {get;} = status; public string Code {get;} = code; public object? Details {get;} = details;
}
public static class Json {
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Encode(object value) => JsonSerializer.Serialize(value,Options);
    public static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value,Options);
}
public static class Security {
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Require(HttpContext ctx, params string[] roles) {
        var id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiError(401,"AUTH_REQUIRED","Please sign in to continue.");
        if(roles.Length>0 && !roles.Any(ctx.User.IsInRole)) throw new ApiError(403,"FORBIDDEN","You do not have permission to do this.");
        return id;
    }
    public static object Profile(User u) => new{u.Id,u.Name,u.Email,u.Role};
    public static string Key(HttpContext ctx) {
        var key = ctx.Request.Headers["Idempotency-Key"].ToString();
        if(string.IsNullOrWhiteSpace(key)||key.Length>128) throw new ApiError(400,"IDEMPOTENCY_REQUIRED","Provide an Idempotency-Key header of at most 128 characters.");
        return key;
    }
    public static void CheckText(string value,int min,int max,string name) {
        if(string.IsNullOrWhiteSpace(value)||value.Trim().Length<min||value.Length>max) throw new ApiError(400,"VALIDATION_ERROR",$"{name} must contain {min}–{max} characters.");
    }
}
public class Store(IDbContextFactory<AppDb> factory) {
    public async Task<T> Write<T>(Func<AppDb,Task<T>> action) {
        for(var attempt=0;;attempt++) {
            await using var db = await factory.CreateDbContextAsync();
            try {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var value = await action(db);
                await db.SaveChangesAsync(); await tx.CommitAsync(); return value;
            } catch(Exception e) when(attempt<5 && Retryable(e)) { await Task.Delay(Random.Shared.Next(20,65)*(attempt+1)); }
        }
    }
    static bool Retryable(Exception e) => e is SqliteException {SqliteErrorCode:5 or 6} || e is SqlException sql && sql.Number is 1205 or 3960 or 2601 or 2627 || e.InnerException!=null && Retryable(e.InnerException);
    public Task<JsonElement> Idempotent(string user,string scope,string key,object request,Func<AppDb,Task<object>> action) => Write(async db=> {
        var fingerprint=Security.Hash(Json.Encode(request));
        var previous = await db.Idempotency.SingleOrDefaultAsync(x=>x.UserId==user&&x.Scope==scope&&x.Key==key);
        if(previous!=null) {
            if(previous.Fingerprint!=fingerprint) throw new ApiError(409,"IDEMPOTENCY_MISMATCH","This request key was already used for different input.");
            return JsonDocument.Parse(previous.Response).RootElement.Clone();
        }
        var result=await action(db);
        var response=Json.Encode(result);
        db.Idempotency.Add(new(){UserId=user,Scope=scope,Key=key,Fingerprint=fingerprint,Response=response});
        return JsonDocument.Parse(response).RootElement.Clone();
    });
    public static void Audit(AppDb db,string actor,string action,string target,string detail="") => db.Audit.Add(new(){ActorId=actor,Action=action,Target=target,Detail=detail});
    public static void Emit(AppDb db,string type,string aggregate,object payload) => db.Outbox.Add(new(){Type=type,AggregateId=aggregate,Payload=Json.Encode(payload)});
    public static void Notify(AppDb db,string user,string title,string body) => db.Notifications.Add(new(){UserId=user,Title=title,Body=body});
}
public static class Telemetry {
    public static readonly Counter Holds = Metrics.CreateCounter("eventhub_hold_attempts_total","Hold attempts");
    public static readonly Counter Conflicts = Metrics.CreateCounter("eventhub_hold_conflicts_total","Seat conflicts");
    public static readonly Histogram HoldLatency = Metrics.CreateHistogram("eventhub_hold_duration_seconds","Hold transaction duration");
}
public class TicketSigner(IConfiguration config, IWebHostEnvironment env) {
    readonly byte[] secret = GetSecret(config,env);
    static byte[] GetSecret(IConfiguration config,IWebHostEnvironment env) {
        var value=config["TicketSigningKey"];
        if(!string.IsNullOrEmpty(value)&&value.Length>=32) return Encoding.UTF8.GetBytes(value);
        if(!env.IsDevelopment()) throw new InvalidOperationException("Set TicketSigningKey to a random value of at least 32 characters.");
        Directory.CreateDirectory("data");
        var path="data/signing.key";
        if(!File.Exists(path)) File.WriteAllBytes(path,RandomNumberGenerator.GetBytes(48));
        return File.ReadAllBytes(path);
    }
    public string Token(Ticket t) {
        var payload=$"{t.Id}.{t.TokenVersion}";
        return payload+"."+Convert.ToHexString(HMACSHA256.HashData(secret,Encoding.UTF8.GetBytes(payload)));
    }
    public bool Valid(string value,Ticket t) {
        var expected=Encoding.UTF8.GetBytes(Token(t)); var actual=Encoding.UTF8.GetBytes(value);
        return expected.Length==actual.Length&&CryptographicOperations.FixedTimeEquals(expected,actual);
    }
}
public class AvailabilityHub : Hub {
    public async Task Subscribe(int eventId) {
        if(eventId>0) await Groups.AddToGroupAsync(Context.ConnectionId,$"event-{eventId}");
    }
    public Task Unsubscribe(int eventId) => Groups.RemoveFromGroupAsync(Context.ConnectionId,$"event-{eventId}");
}
