using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace VectorSpace.Collaboration;

public sealed record RoomAddress(string Server, string RoomId, string Token)
{
    public Uri Endpoint()
    {
        if (!Uri.TryCreate(Server, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) throw new ArgumentException("Use an HTTPS server address, or HTTP loopback for local development.");
        if (RoomId.Length != 32 || !RoomId.All(Uri.IsHexDigit) || Token.Length is < 32 or > 256) throw new ArgumentException("Invalid room invitation.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
    public string Link(string applicationUrl = "https://wieslawsoltes.github.io/VectorSpace/")
    {
        _ = Endpoint();
        var json = new JsonObject { ["server"] = Server, ["room"] = RoomId, ["token"] = Token }.ToJsonString();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return applicationUrl.Split('#')[0] + "#collaboration=" + encoded;
    }
    public static RoomAddress Parse(string link)
    {
        if (link.Length > 8192) throw new ArgumentException("Invitation link is too long.");
        var index = link.IndexOf("#collaboration=", StringComparison.Ordinal);
        if (index < 0) throw new ArgumentException("Paste a VectorSpace collaboration invitation link.");
        var encoded = link[(index + 15)..].Trim().Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        var value = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))) as JsonObject ?? throw new ArgumentException("Invalid invitation link.");
        var address = new RoomAddress(value["server"]?.GetValue<string>() ?? "", value["room"]?.GetValue<string>() ?? "", value["token"]?.GetValue<string>() ?? "");
        _ = address.Endpoint(); return address;
    }
}

/// <summary>Credentials are sent in headers, never URL query parameters. Redirects are disabled.
/// Instances own their HttpClient; concurrent send, poll and coalesced presence requests are supported.</summary>
public sealed class RoomTransport : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _path;
    public RoomAddress Address { get; }
    public RoomTransport(RoomAddress address)
    {
        Address = address;
        _http = NewClient(); _http.BaseAddress = address.Endpoint();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", address.Token);
        _path = "api/rooms/" + address.RoomId;
    }
    private static HttpClient NewClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        if (!OperatingSystem.IsBrowser()) handler.UseCookies = false;
        return new(handler) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 96L * 1024 * 1024 };
    }
    public static async Task<RoomAddress> CreateAsync(string server, string key, string name, string document, CancellationToken cancellationToken = default)
    {
        var temporary = new RoomAddress(server, new string('0', 32), new string('0', 43));
        using var http = NewClient(); http.BaseAddress = temporary.Endpoint();
        if (key.Length is < 32 or > 256) throw new ArgumentException("Enter the service's room-creation key.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/rooms");
        request.Headers.Add("X-VectorSpace-Create-Key", key);
        request.Content = JsonContent.Create(new CreateRoom(document, name), CollaborationJson.Default.CreateRoom);
        using var response = await http.SendAsync(request, cancellationToken);
        var invite = await Read(response, CollaborationJson.Default.Invitation, cancellationToken);
        return new(server, invite.RoomId, invite.Token);
    }
    public Task<SyncReply> SyncAsync(long revision, long afterEvent, bool wait, CancellationToken token) =>
        Get(_path + $"/sync?since={revision}&afterEvent={afterEvent}&wait={wait.ToString().ToLowerInvariant()}", CollaborationJson.Default.SyncReply, token);
    public Task<SyncReply> SubmitAsync(EditBatch batch, CancellationToken token) =>
        Post(_path + "/edits", batch, CollaborationJson.Default.EditBatch, CollaborationJson.Default.SyncReply, token);
    public Task<RoomInfo> PresenceAsync(Participant participant, CancellationToken token) =>
        Post(_path + "/presence", participant, CollaborationJson.Default.Participant, CollaborationJson.Default.RoomInfo, token);
    public Task<Invitation> InviteAsync(string label, RoomRole role, CancellationToken token = default) =>
        Post(_path + "/invitations", new CreateInvitation(label, role), CollaborationJson.Default.CreateInvitation, CollaborationJson.Default.Invitation, token);
    public Task<List<RoomGrant>> InvitationsAsync(CancellationToken token = default) => Get(_path + "/invitations", CollaborationJson.Default.ListRoomGrant, token);
    public Task<List<HistoryItem>> HistoryAsync(CancellationToken token = default) => Get(_path + "/history", CollaborationJson.Default.ListHistoryItem, token);
    public async Task<string> VersionAsync(long revision, CancellationToken token = default)
    {
        using var response = await _http.GetAsync(_path + "/versions/" + revision, token); await Ensure(response, token); return await response.Content.ReadAsStringAsync(token);
    }
    public Task RevokeAsync(string id, CancellationToken token = default) => Delete(_path + "/invitations/" + Uri.EscapeDataString(id), token);
    public Task LeaveAsync(string client, CancellationToken token = default) => Delete(_path + "/presence/" + Uri.EscapeDataString(client), token);
    private async Task Delete(string path, CancellationToken token) { using var response = await _http.DeleteAsync(path, token); await Ensure(response, token); }
    private async Task<T> Get<T>(string path, JsonTypeInfo<T> info, CancellationToken token)
    { using var response = await _http.GetAsync(path, token); return await Read(response, info, token); }
    private async Task<TOut> Post<TIn, TOut>(string path, TIn value, JsonTypeInfo<TIn> input, JsonTypeInfo<TOut> output, CancellationToken token)
    { using var body = JsonContent.Create(value, input); using var response = await _http.PostAsync(path, body, token); return await Read(response, output, token); }
    private static async Task<T> Read<T>(HttpResponseMessage response, JsonTypeInfo<T> info, CancellationToken token)
    {
        await Ensure(response, token);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await JsonSerializer.DeserializeAsync(stream, info, token) ?? throw new InvalidDataException("The collaboration server returned an empty response.");
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync(token);
        if (text.Length > 512) text = text[..512];
        throw new HttpRequestException("Collaboration service: " + text, null, response.StatusCode);
    }
    public void Dispose() => _http.Dispose();
}
