using System.Globalization;
using VectorSpace.Collaboration;
using VectorSpace.Core;

namespace VectorSpace.Server;

internal sealed partial class Room
{
    private void UpgradeNativeFormat()
    {
        var key = DocumentProjection.Key("$root", "formatVersion");
        if (!int.TryParse(_engine.State.Value(key), NumberStyles.None, CultureInfo.InvariantCulture, out var format))
            throw new InvalidDataException("Invalid persisted native format version.");
        if (format == DesignDocument.CurrentFormatVersion) return;
        // Replay first, then migrate as one durable system revision. Never rewrite old journal
        // frames or reset receipts: reconnect, historical downloads and attribution remain intact.
        var document = DocumentProjection.ToDocument(_engine.State);
        ServerDocument.Normalize(document);
        var migrated = DocumentProjection.FromDocument(document, _engine.State);
        var changes = DocumentProjection.Diff(_engine.State, migrated);
        var revision = checked(_engine.State.Revision + 1);
        var id = "native-schema-" + DesignDocument.CurrentFormatVersion;
        var commit = new Commit(revision, id, "system", 1, "System", "Upgrade native document schema", DateTimeOffset.UtcNow, changes);
        var receipt = new Receipt(id, 1, true, revision, null);
        var record = new JournalRecord("system/" + id, receipt, commit);
        RoomJournal.Append(_journalPath, record);
        _engine.Accept(commit); Remember(commit); _receipts[record.Actor] = record;
    }
}
