"""One-time reviewed finalization, removed after validation and commit."""
from pathlib import Path

def replace(path, old, new, count=1):
    p = Path(path); text = p.read_text()
    assert text.count(old) == count, (path, old[:100], text.count(old))
    p.write_text(text.replace(old, new))

surface = 'src/VectorSpace.Editor/DesignSurface.cs'
replace(surface, '''        _canvas.PointerPressed += (sender, e) =>
        {
            try { Pressed(sender, e); }
            catch (InvalidOperationException error) { CancelGesture(); StatusChanged?.Invoke(error.Message); }
        }; _canvas.PointerMoved += Moved; _canvas.PointerReleased += Released;''', '''        _canvas.PointerPressed += (sender, e) => ExecuteInput(() => Pressed(sender, e));
        _canvas.PointerMoved += (sender, e) => ExecuteInput(() => Moved(sender, e));
        _canvas.PointerReleased += (sender, e) => ExecuteInput(() => Released(sender, e));''')
replace(surface, '_canvas.PointerWheelChanged += Wheel;', '_canvas.PointerWheelChanged += (sender, e) => ExecuteInput(() => Wheel(sender, e));')
replace(surface, '_canvas.DoubleTapped += (_, e) =>\n        {', '_canvas.DoubleTapped += (_, e) => ExecuteInput(() =>\n        {')
replace(surface, '        };\n        _canvas.RightTapped', '        });\n        _canvas.RightTapped')
replace(surface, 'try { if (commit) Session?.CommitInteraction(); else Session?.CancelInteraction(); }', 'try { ExecuteInput(() => { if (commit) Session?.CommitInteraction(); else Session?.CancelInteraction(); }); }')

connection = 'src/VectorSpace.Collaboration/CollaborationConnection.cs'
replace(connection, '''        Role = reply.Role; _event = reply.Event; Online = true;
        Status = Replica.LastError ?? (Replica.PendingCount == 0 ? "All changes synchronized" : $"Synchronizing {Replica.PendingCount} edit(s)");''', '''        Role = reply.Role; _event = reply.Event;
        // A response queued at a local edit boundary can arrive after revocation.
        // Apply acknowledged data without advertising restored access from that response.
        Online = !AccessDenied;
        if (!AccessDenied)
            Status = Replica.LastError ?? (Replica.PendingCount == 0 ? "All changes synchronized" : $"Synchronizing {Replica.PendingCount} edit(s)");''')
replace('src/VectorSpace.Workbench/StudioWorkbench.SharedRecovery.cs', '{r.Confirmed.Revision}:{r.LastSequence}:{r.PendingCount}:{r.Recovery.Count}', '{r.LastSequence}:{r.PendingCount}:{r.Recovery.Count}')
replace('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs', 'json.WriteString("id", primary?.Id);', 'json.WriteString("text", primary?.Text); json.WriteBoolean("textEditing", workbench.Surface.IsTextEditing);\n                json.WriteString("id", primary?.Id);')
replace('tests/VectorSpace.Collaboration.EditorTests/Program.cs', 'var failures = 0;', '''Test("shared commit rejected after an active gesture can roll back without losing selection", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.Select(e.Document.Find("a"));
    e.BeginInteraction("Drag"); e.Primary!.X = 99; h.Fail = true;
    Throws<InvalidOperationException>(() => e.CommitInteraction());
    Check(e.IsInteracting); e.CancelInteraction();
    Check(!e.IsInteracting && e.Primary!.X == 0 && e.Primary.Id == "a" && h.Commits.Count == 0);
});

var failures = 0;''')

p = Path('docs/VALIDATION.md'); text = p.read_text().replace('**21 cases**', '**23 cases**').replace('**44 Chromium cases**', '**46 Chromium cases**').replace('nine tagged', 'eleven tagged').replace('nine collaboration cases', 'eleven collaboration cases').replace('The nine', 'The eleven')
text = text.replace('version download/restore and active-access revocation.', 'version download/restore and active-access revocation, including revocation during an active pointer or inline-text edit. The shared-editor runner includes 59 seeded projection equivalence and key/value reuse assertions.')
p.write_text(text)
replace('CHANGELOG.md', '# Changelog\n', '''# Changelog

## 0.6.0-alpha.1 — Collaborative editing

- Added the tenth reusable library, `VectorSpace.Collaboration`, and a persistent ASP.NET room service with guarded property/hierarchy transactions, idempotent retries, normalized layout/variables/components, and conditional own undo/redo.
- Added real Share/create/join/invite/revoke flows, server-enforced viewer/commenter/editor/owner capabilities, synchronized comments, retained remote cursors and selections, participant following, and revision download/restore.
- Added separate token-free browser/desktop recovery journals for unacknowledged or rejected work. Remote model updates respect pointer, inline-text and modal transaction boundaries; revoked mid-gesture edits roll back without escaping the input event loop.
- Replaced mutable-JSON projection with a read-only DOM and retained unchanged cell keys/values. A test-only reference oracle verifies equivalence; benchmarks report projection cost and exact delta wire size without an application-wide speed claim.
- Added actual multi-window browser and HTTP/restart acceptance, projection equivalence checks, server/container packaging and collaboration-aware build/release gates. Corrected duplicate tracing, asynchronous clipboard and stale inspector diagnostics in browser tests.
- Added collaboration/hosting documentation and refreshed architecture/security guidance. Pages remains a static client; cross-device collaboration requires a separately operated HTTPS backend with persistent disk. Native document schema remains 4.
''')
p = Path('docs/PERFORMANCE.md'); p.write_text(p.read_text() + '''
## Collaborative projection and presence

The production projection uses a read-only `JsonDocument` and one reusable per-call UTF-8 writer rather than constructing a second mutable JSON tree. Span-based dictionary lookup reuses existing cell addresses; unchanged canonical property strings retain their original instances. A test-only copy of the prior projection independently verifies output across 59 equivalence/reuse cases, including Unicode, hierarchy reordering and seeded edits.

```bash
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release -- --benchmark
```

A local Linux x64/.NET 10.0.12 run used 1,000 native layers, one scalar edit and five interleaved warmed samples per implementation:

| Projection/diff measurement | Prior mutable JSON | Retained reader |
|---|---:|---:|
| Median elapsed | 99.524 ms | 48.3378 ms |
| Managed bytes allocated | 28,405,816 | 4,552,184 |

The full native document was 1,732,800 UTF-8 bytes; its exact one-cell edit batch was 209 bytes. The delta reconstructed the exact native document. [Raw local record](benchmarks/collaboration-projection.json); CI retains independent measurements rather than enforcing machine-dependent timing thresholds.

This measures projection/diff only: no network latency, journal flush, UI dispatch, render, native allocation or embedded-image workload is included. Projection still visits the entire document. A twofold observed projection speed ratio is not an application-wide speedup.

Presence does not enter undo history or trigger document projection. Client samples and room wakeups are coalesced; cursor painting uses a separate retained overlay. Unchanged follow-viewports do not cause another editor viewport notification, and remote-only revisions do not rewrite an unchanged local recovery journal. Full canonicalization, snapshots, retained revision/receipt state and recovery documents still consume workload-dependent memory. See [collaboration bounds](COLLABORATION.md).
''')
p = Path('.gitignore'); p.write_text(p.read_text() + '\n# Python validation caches\n__pycache__/\n*.py[cod]\n')
print('All exact collaboration finalization changes applied.')
