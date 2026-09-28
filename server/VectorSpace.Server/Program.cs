using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VectorSpace.Collaboration;
using VectorSpace.Server;

var builder = WebApplication.CreateBuilder(args);
var createKey = builder.Configuration["VECTORSPACE_CREATE_KEY"];
if (string.IsNullOrWhiteSpace(createKey) || createKey.Length < 32) throw new InvalidOperationException("Set VECTORSPACE_CREATE_KEY to an administrator-generated secret of at least 32 characters.");
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://127.0.0.1:5097");
builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 96L * 1024 * 1024; o.Limits.MaxConcurrentConnections = 256; });
var origins = (builder.Configuration["VECTORSPACE_ALLOWED_ORIGINS"] ?? "http://127.0.0.1:4173,http://localhost:4173,https://wieslawsoltes.github.io").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (origins.Any(o => !Uri.TryCreate(o, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http") || u.GetLeftPart(UriPartial.Authority) != o || u.UserInfo.Length > 0)) throw new InvalidOperationException("Configure exact allowed origins, without paths or wildcards.");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).WithMethods("GET", "POST", "DELETE").WithHeaders("Authorization", "Content-Type", "X-VectorSpace-Create-Key")));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("api", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 60000, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
using var repository = new RoomRepository(builder.Configuration["VECTORSPACE_DATA"] ?? Path.Combine(AppContext.BaseDirectory, "data"));
var app = builder.Build();
app.Use(async (context, next) => {
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    try { await next(); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception error) when (!context.Response.HasStarted && error is UnauthorizedAccessException or KeyNotFoundException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException or IOException)
    {
        context.Response.StatusCode = error switch { UnauthorizedAccessException => 403, KeyNotFoundException => 404, IOException => 503, _ => 400 };
        await context.Response.WriteAsync(error is IOException ? "Persistent storage is unavailable; no edit was acknowledged." : error.Message);
    }
});
app.UseCors(); app.UseRateLimiter();
app.MapGet("/health", () => Results.Text("VectorSpace collaboration service ready"));
var api = app.MapGroup("/api/rooms").RequireRateLimiting("api");
api.AddEndpointFilter(async (context, next) =>
{
    // Reject unauthorized callers before reading a potentially large JSON body.
    // Mutation handlers recheck the invitation under the room lock after reading.
    var request = context.HttpContext;
    if (request.Request.RouteValues["id"] is string id)
    {
        var room = repository.Get(id); await room.Gate.WaitAsync(request.RequestAborted);
        try { _ = room.Authorize(Token(request)); } finally { room.Gate.Release(); }
    }
    return await next(context);
});
api.MapPost("", async (HttpContext c) => {
    var supplied = c.Request.Headers["X-VectorSpace-Create-Key"].ToString();
    if (supplied.Length > 256 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(createKey)))) throw new UnauthorizedAccessException("A valid room-creation key is required.");
    var request = await Read(c, CollaborationJson.Default.CreateRoom);
    return Results.Json(repository.Create(request), CollaborationJson.Default.Invitation);
});
api.MapGet("/{id}/sync", async (HttpContext c, string id, long since = -1, long afterEvent = -1, bool wait = false) => {
    var room = repository.Get(id); var token = Token(c); Task? changed = null;
    await room.Gate.WaitAsync(c.RequestAborted);
    try {
        _ = room.Authorize(token);
        if (wait && room.Revision == since && room.Event == afterEvent) changed = room.Changed;
    } finally { room.Gate.Release(); }
    if (changed is not null) { try { await changed.WaitAsync(TimeSpan.FromSeconds(20), c.RequestAborted); } catch (TimeoutException) { } }
    return await InRoom(c, id, r => r.Sync(r.Authorize(token), since), CollaborationJson.Default.SyncReply);
});
api.MapPost("/{id}/edits", async (HttpContext c, string id) => {
    var request = await Read(c, CollaborationJson.Default.EditBatch);
    return await InRoom(c, id, r => r.Submit(r.Authorize(Token(c)), request), CollaborationJson.Default.SyncReply);
});
api.MapPost("/{id}/presence", async (HttpContext c, string id) => {
    var request = await Read(c, CollaborationJson.Default.Participant);
    return await InRoom(c, id, r => { var g = r.Authorize(Token(c)); r.Presence(g, request); return new RoomInfo(r.Id, g.Role, r.Name); }, CollaborationJson.Default.RoomInfo);
});
api.MapDelete("/{id}/presence/{client}", async (HttpContext c, string id, string client) =>
    await InRoom(c, id, r => { var g = r.Authorize(Token(c)); r.Leave(g, client); return new RoomInfo(r.Id, g.Role, r.Name); }, CollaborationJson.Default.RoomInfo));
api.MapPost("/{id}/invitations", async (HttpContext c, string id) => {
    var request = await Read(c, CollaborationJson.Default.CreateInvitation);
    return await InRoom(c, id, r => r.Invite(r.Authorize(Token(c)), request), CollaborationJson.Default.Invitation);
});
api.MapGet("/{id}/invitations", async (HttpContext c, string id) =>
    await InRoom(c, id, r => r.Grants(r.Authorize(Token(c))), CollaborationJson.Default.ListRoomGrant));
api.MapDelete("/{id}/invitations/{grant}", async (HttpContext c, string id, string grant) =>
    await InRoom(c, id, r => { var g = r.Authorize(Token(c)); r.Revoke(g, grant); return new RoomInfo(r.Id, g.Role, r.Name); }, CollaborationJson.Default.RoomInfo));
api.MapGet("/{id}/history", async (HttpContext c, string id) =>
    await InRoom(c, id, r => { _ = r.Authorize(Token(c)); return r.History(); }, CollaborationJson.Default.ListHistoryItem));
api.MapGet("/{id}/versions/{revision:long}", async (HttpContext c, string id, long revision) => {
    var room = repository.Get(id); await room.Gate.WaitAsync(c.RequestAborted);
    try { _ = room.Authorize(Token(c)); return Results.Text(room.Version(revision), "application/json"); } finally { room.Gate.Release(); }
});
await app.RunAsync();

static string Token(HttpContext c)
{
    var header = c.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || header.Length > 263) throw new UnauthorizedAccessException("A bearer invitation is required.");
    return header[7..];
}
static async Task<T> Read<T>(HttpContext c, JsonTypeInfo<T> type) where T : class =>
    await JsonSerializer.DeserializeAsync(c.Request.Body, type, c.RequestAborted) ?? throw new InvalidDataException("A JSON request is required.");
async Task<IResult> InRoom<T>(HttpContext c, string id, Func<Room, T> action, JsonTypeInfo<T> type)
{
    var room = repository.Get(id); await room.Gate.WaitAsync(c.RequestAborted);
    try { _ = room.Authorize(Token(c)); return Results.Json(action(room), type); }
    finally { room.Gate.Release(); }
}
