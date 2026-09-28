"""One-time source integration. Every replacement is asserted; remove after applying."""
from pathlib import Path

def replace(path, old, new, count=1):
    p = Path(path); text = p.read_text()
    assert text.count(old) == count, (path, old[:100], text.count(old), count)
    p.write_text(text.replace(old, new))

editor = 'src/VectorSpace.Editing/EditorSession.cs'
replace(editor, 'public bool CanUndo => _undo.Count > 0;', 'public bool CanUndo => SharedHistory?.CanUndo ?? (_undo.Count > 0);')
replace(editor, 'public bool CanRedo => _redo.Count > 0;', 'public bool CanRedo => SharedHistory?.CanRedo ?? (_redo.Count > 0);')
replace(editor, 'public string UndoLabel => _undo.LastOrDefault()?.Label ?? "";', 'public string UndoLabel => SharedHistory?.UndoLabel ?? _undo.LastOrDefault()?.Label ?? "";')
replace(editor, 'public string RedoLabel => _redo.TryPeek(out var item) ? item.Label : "";', 'public string RedoLabel => SharedHistory?.RedoLabel ?? (_redo.TryPeek(out var item) ? item.Label : "");')
replace(editor, 'public IReadOnlyList<string> History => _undo.Select(e => e.Label).ToArray();', 'public IReadOnlyList<string> History => SharedHistory?.History ?? _undo.Select(e => e.Label).ToArray();')
replace(editor, 'public void Load(DesignDocument document)\n    {', 'public void Load(DesignDocument document)\n    {\n        if (SharedHistory is not null) throw new InvalidOperationException("Leave the shared file before opening another document.");')
replace(editor, 'public void BeginInteraction(string label)\n    {', 'public void BeginInteraction(string label)\n    {\n        if (SharedHistory?.CanEdit(label) == false) throw new InvalidOperationException("This shared file is read-only for this action, or its synchronization queue is full.");')
replace(editor, '''        var after = Capture(); var before = _before; _before = null;
        if (before.Json != after.Json)
        {
            _undo.Add(new(_interactionLabel, before, after)); _redo.Clear();
            while (_undo.Count > 150 || (_undo.Count > 1 && _undo.Sum(x => (long)x.Before.Json.Length + x.After.Json.Length) > 32 * 1024 * 1024)) _undo.RemoveAt(0);
            IsDirty = after.Json != _savedJson;
        }
        Notify(EditorChangeKind.Document, _interactionLabel);''', '''        var after = Capture(); var before = _before;
        if (before.Json != after.Json)
        {
            if (SharedHistory is { } shared) shared.Commit(_interactionLabel, before.Json, after.Json);
            else
            {
                _undo.Add(new(_interactionLabel, before, after)); _redo.Clear();
                while (_undo.Count > 150 || (_undo.Count > 1 && _undo.Sum(x => (long)x.Before.Json.Length + x.After.Json.Length) > 32 * 1024 * 1024)) _undo.RemoveAt(0);
            }
            IsDirty = after.Json != _savedJson;
        }
        _before = null;
        Notify(EditorChangeKind.Document, _interactionLabel);''')
replace(editor, 'if (IsInteracting) { CancelInteraction(); return; }\n        if (_undo.Count == 0)', 'if (IsInteracting) { CancelInteraction(); return; }\n        if (SharedHistory is { } shared) { shared.Undo(); return; }\n        if (_undo.Count == 0)')
replace(editor, 'if (IsInteracting || !_redo.TryPop(out var entry)) return;', 'if (IsInteracting) return;\n        if (SharedHistory is { } shared) { shared.Redo(); return; }\n        if (!_redo.TryPop(out var entry)) return;')

replica = 'src/VectorSpace.Collaboration/SharedReplica.cs'
replace(replica, 'public int PendingCount => _pending.Count;', 'public int PendingCount => _pending.Count;\n    public string? PendingDocument => _pending.LastOrDefault()?.Document;\n    public long LastSequence => _sequence;')
replace('src/VectorSpace.Collaboration/CollaborationConnection.Loops.cs', 'await _dispatch(() => Receive(reply));', 'await Deliver(reply);', 2)
replace('src/VectorSpace.Collaboration/CollaborationConnection.cs', 'HttpStatusCode.NotFound }', 'HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge }')
replace('src/VectorSpace.Controls/ParticipantStrip.cs', 'Spacing = -5;', 'Spacing = 3;')

workbench = 'src/VectorSpace.Workbench/StudioWorkbench.cs'
replace(workbench, 'Content = _root;\n        Session.Changed', 'Content = _root;\n        InitializeCollaboration(canvasArea);\n        Session.Changed')
old = '''        var avatar = new Border { Width = 28, Height = 28, CornerRadius = new(14), Background = Studio.Brush("#F5D7A6"), Child = Studio.Text("Y", 11, "#775719", true), Padding = new(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(avatar, "You · Local editing");'''
replace(workbench, old, '        var avatar = _participants;')
replace(workbench, 'Studio.Columns((avatar, 28), (new Grid(), -1), (present, 30), (share, 70))', 'Studio.Columns((avatar, -1), (present, 30), (share, 70))')
replace(workbench, '_status.Text = "All changes saved locally";', '_status.Text = _collaboration?.Status ?? "All changes saved locally";')
replace(workbench, 'private async void RunAsync(Func<Task> action)\n    {\n        try', 'private async void RunAsync(Func<Task> action)\n    {\n        _sharedAsyncDepth++;\n        try')
replace(workbench, '''        try { Surface.FinishTextEdit(true); Surface.FinishPath(false); await action(); }
        catch (Exception ex) { ShowStatus(ex.Message, true); }
    }''', '''        try { Surface.FinishTextEdit(true); Surface.FinishPath(false); await action(); }
        catch (Exception ex) { ShowStatus(ex.Message, true); }
        finally { _sharedAsyncDepth--; DispatcherQueue.TryEnqueue(FlushRemoteDeliveries); }
    }''')
replace(workbench, '_disposed = true; Session.Changed -= OnSessionChanged;', '_disposed = true; DisposeCollaboration(); Session.Changed -= OnSessionChanged;')

commands = 'src/VectorSpace.Workbench/StudioWorkbench.Commands.cs'
replace(commands, 'private async Task ShowShareAsync()', 'private async Task ShowLocalShareAsync()')
replace(commands, 'foreach (var action in EditingActions()) yield return action;', '''foreach (var action in EditingActions()) yield return action;
        yield return new("Share file", "", () => RunAsync(ShowShareAsync));
        yield return new("People in this file", "", () => RunAsync(ShowParticipantsAsync));
        yield return new("Version history", "", () => RunAsync(ShowSharedHistoryAsync));
        yield return new("Local collaboration recovery", "", () => RunAsync(ShowSharedRecoveryAsync));''')
replace(commands, 'string close = "Close") => new()', 'string close = "Close") => TrackSharedDialog(new()')
replace(commands, 'MinWidth = 320, MaxWidth = 560\n    };', 'MinWidth = 320, MaxWidth = 560\n    });')
replace(commands, 'PageId = Session.Page.Id, Anchor = anchor, Text = text', 'PageId = Session.Page.Id, Anchor = anchor, Text = text, Author = _collaboration is null ? "You" : _participantName')
replace(commands, 'Wrapped("You: " + reply, 12, Studio.Ink)', 'Wrapped(reply, 12, Studio.Ink)')
replace(commands, 'thread.Replies.Add(input.Text.Trim())', 'thread.Replies.Add((_collaboration is null ? "You" : _participantName) + ": " + input.Text.Trim())')
replace(commands, '.fig files, multiplayer, remote libraries, plugins, rich text and full vector networks remain unavailable.', '.fig files, remote libraries, plugins, rich text and full vector networks remain unavailable. Multi-user editing requires a separately configured collaboration service.')

surface = 'src/VectorSpace.Editor/DesignSurface.cs'
replace(surface, '_canvas.PointerPressed += Pressed;', '''_canvas.PointerPressed += (sender, e) =>
        {
            try { Pressed(sender, e); }
            catch (InvalidOperationException error) { CancelGesture(); StatusChanged?.Invoke(error.Message); }
        };''')
replace(surface, 'if (Session is null || IsPresenting || IsImageCropping) return;', 'if (Session is null || IsPresenting || IsImageCropping || Session.SharedHistory?.CanEdit("Edit layer") == false) return;')
replace(surface, 'if (PressVectorEdit(screen, world, shift)) return;', '''if (editor.SharedHistory?.CanEdit("Edit layer") == false)
        {
            editor.Select(Hit(world, screen, Keyboard.Control), shift); return;
        }
        if (PressVectorEdit(screen, world, shift)) return;''')

proj = 'src/VectorSpace.Workbench/VectorSpace.Workbench.csproj'
replace(proj, '<ProjectReference Include="../VectorSpace.Editor/VectorSpace.Editor.csproj" />', '<ProjectReference Include="../VectorSpace.Editor/VectorSpace.Editor.csproj" /><ProjectReference Include="../VectorSpace.Collaboration/VectorSpace.Collaboration.csproj" />')
replace('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs', 'internal sealed class BrowserWorkspaceStorage', 'internal sealed partial class BrowserWorkspaceStorage')
replace('src/VectorSpace.App/Platforms/Desktop/DesktopWorkspaceStorage.cs', 'internal sealed class DesktopWorkspaceStorage', 'internal sealed partial class DesktopWorkspaceStorage')
replace('src/VectorSpace.App/Platforms/WebAssembly/BrowserWorkspaceStorage.cs', 'json.WriteStartObject(); json.WriteBoolean("ready", true);', 'json.WriteStartObject(); workbench.WriteCollaborationDiagnostics(json); json.WriteBoolean("ready", true);')
replace('src/VectorSpace.Workbench/StudioWorkbench.Automation.cs', 'if (view is TextBox textBox) json.WriteString', 'if (view is TextBox textBox && textBox.Tag as string != "Sensitive") json.WriteString')
replace('src/VectorSpace.App/VectorSpace.App.csproj', '<EmbeddedResource Include="Platforms/WebAssembly/WasmScripts/Storage.js" />', '<EmbeddedResource Include="Platforms/WebAssembly/WasmScripts/Storage.js" />\n    <EmbeddedResource Include="Platforms/WebAssembly/WasmScripts/Collaboration.js" />')
replace('src/VectorSpace.App/App.xaml.cs', 'BrowserDiagnostics.Attach(session, _workbench);', '''BrowserDiagnostics.Attach(session, _workbench);
            _workbench.CollaborationApplicationUrl = BrowserSharedFiles.ApplicationUrl();
            var invitation = BrowserSharedFiles.TakeInvitation();
            if (!string.IsNullOrWhiteSpace(invitation)) _workbench.OpenCollaborationInvitation(invitation);''')
replace('Directory.Build.props', '<Version>0.5.0-alpha.1</Version>', '<Version>0.6.0-alpha.1</Version>')
replace('src/VectorSpace.App/VectorSpace.App.csproj', '<ApplicationDisplayVersion>0.5.0</ApplicationDisplayVersion>', '<ApplicationDisplayVersion>0.6.0</ApplicationDisplayVersion>')
replace('src/VectorSpace.App/VectorSpace.App.csproj', '<ApplicationVersion>5</ApplicationVersion>', '<ApplicationVersion>6</ApplicationVersion>')
print('All guarded integration edits applied.')
