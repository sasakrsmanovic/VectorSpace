using System.Security.Cryptography;
using System.Text;
using VectorSpace.Collaboration;

namespace VectorSpace.Server;

internal sealed partial class Room
{
    public GrantRecord Authorize(string token)
    {
        if (token.Length is < 32 or > 256) throw new UnauthorizedAccessException("A valid room invitation is required.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        foreach (var grant in _metadata.Grants)
            if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(grant.TokenHash))) return grant;
        throw new UnauthorizedAccessException("This invitation is invalid or has been revoked.");
    }
    public static (string Token, GrantRecord Grant) NewGrant(string label, RoomRole role)
    {
        if (!Enum.IsDefined(role) || string.IsNullOrWhiteSpace(label) || label.Length > 64 || label.Any(char.IsControl)) throw new InvalidDataException("Invalid invitation label or role.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, new(Guid.NewGuid().ToString("N"), label.Trim(), role, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), DateTimeOffset.UtcNow));
    }
    public Invitation Invite(GrantRecord caller, CreateInvitation request)
    {
        if (caller.Role != RoomRole.Owner) throw new UnauthorizedAccessException("Only the room owner can create invitations.");
        if (request.Role == RoomRole.Owner || _metadata.Grants.Count >= 128) throw new InvalidDataException("Choose a non-owner role; rooms support 127 guest invitations.");
        var (token, grant) = NewGrant(request.Label, request.Role);
        var next = _metadata with { Grants = [.. _metadata.Grants, grant] };
        RoomJournal.SaveMetadata(_metadataPath, next); _metadata = next; Pulse();
        return new(Id, token, grant.Role, grant.Id);
    }
    public List<RoomGrant> Grants(GrantRecord caller)
    {
        if (caller.Role != RoomRole.Owner) throw new UnauthorizedAccessException("Only the owner can inspect invitations.");
        return _metadata.Grants.Select(g => new RoomGrant(g.Id, g.Label, g.Role, g.CreatedAt)).ToList();
    }
    public void Revoke(GrantRecord caller, string id)
    {
        if (caller.Role != RoomRole.Owner) throw new UnauthorizedAccessException("Only the owner can revoke invitations.");
        if (id == caller.Id) throw new InvalidDataException("The room owner invitation cannot be revoked here.");
        var next = _metadata with { Grants = _metadata.Grants.Where(g => g.Id != id).ToList() };
        RoomJournal.SaveMetadata(_metadataPath, next); _metadata = next;
        foreach (var key in _participants.Where(p => p.Value.Grant == id).Select(p => p.Key).ToArray()) _participants.Remove(key);
        Pulse();
    }
    public void Presence(GrantRecord grant, Participant p)
    {
        PurgePresence();
        if (p.ClientId is null || p.ClientId.Length != 32 || !p.ClientId.All(Uri.IsHexDigit) || p.Name is null || p.Name.Length is < 1 or > 64 || p.Name.Any(char.IsControl) || p.PageId is null || p.PageId.Length > 256 ||
            !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.PanX) || !double.IsFinite(p.PanY) || !double.IsFinite(p.Zoom) || p.Zoom is < .02 or > 64 ||
            Math.Abs(p.X) > 1e12 || Math.Abs(p.Y) > 1e12 || Math.Abs(p.PanX) > 1e12 || Math.Abs(p.PanY) > 1e12 || p.Selection is null || p.Selection.Count > 100 || p.Selection.Any(x => x is null || x.Length > 256))
            throw new InvalidDataException("Invalid participant presence.");
        if (!_participants.ContainsKey(p.ClientId) && _participants.Count >= 32) throw new InvalidOperationException("This room already has 32 active participants.");
        if (_participants.TryGetValue(p.ClientId, out var old) && old.Grant != grant.Id) throw new UnauthorizedAccessException("Participant identity belongs to another invitation.");
        string[] colors = ["#0D99FF", "#9747FF", "#F24822", "#14AE5C", "#E38B00", "#D94EAB"];
        var color = colors[SHA256.HashData(Encoding.UTF8.GetBytes(p.ClientId))[0] % colors.Length];
        _participants[p.ClientId] = (grant.Id, p with { Color = color, SeenAt = DateTimeOffset.UtcNow }); QueuePresenceNotification();
    }
}
