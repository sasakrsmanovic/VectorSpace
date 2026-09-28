using Windows.ApplicationModel.DataTransfer;
using VectorSpace.Collaboration;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private async Task ShowShareAsync()
    {
        if (_collaboration is not null) { await ShowConnectedShareAsync(); return; }
        var root = new StackPanel { Spacing = 10, Width = 410 };
        root.Children.Add(Wrapped("Work together with live cursors, shared edits and comments. Your collaboration server stores this file; GitHub Pages only hosts the editor.", 12, Studio.Ink));
        var name = Studio.Input(_participantName, "Collaboration display name"); name.PlaceholderText = "Your display name"; name.MaxLength = 64;
        root.Children.Add(Studio.Text("Your name", 11, Studio.Ink, true)); root.Children.Add(name);
        root.Children.Add(Studio.Rule()); root.Children.Add(Studio.Text("Join an existing file", 12, Studio.Ink, true));
        var invitation = Studio.Input(PendingInvitation ?? "", "Collaboration invitation link"); invitation.PlaceholderText = "Paste a VectorSpace invitation link"; invitation.Height = 62; invitation.TextWrapping = TextWrapping.Wrap; invitation.Tag = "Sensitive"; invitation.MaxLength = 8192;
        root.Children.Add(invitation);
        root.Children.Add(Studio.Rule()); root.Children.Add(Studio.Text("Create a shared file", 12, Studio.Ink, true));
        var server = Studio.Input("http://127.0.0.1:5097", "Collaboration server"); server.PlaceholderText = "https://your-collaboration-server"; root.Children.Add(server);
        var key = new PasswordBox { PlaceholderText = "Server room-creation key", MaxLength = 256, FontFamily = Studio.Font };
        AutomationProperties.SetName(key, "Collaboration creation key"); root.Children.Add(key);
        root.Children.Add(Wrapped("Creating uploads the current design to the server you specify. Joining replaces this editor's document. Save a local copy first. Invitation links grant access to anyone who holds them; no credentials are stored automatically.", 10));
        var dialog = Dialog("Share file", Studio.Scroll(root), "Join file", "Cancel"); dialog.SecondaryButtonText = "Create shared file";
        var recover = new StudioButton("Local collaboration recovery", () => { }) { HorizontalAlignment = HorizontalAlignment.Left };
        var recoveryRequested = false; recover.Click += (_, _) => { recoveryRequested = true; dialog.Hide(); }; root.Children.Add(recover);
        var result = await dialog.ShowAsync();
        if (recoveryRequested) { await ShowSharedRecoveryAsync(); return; }
        if (result == ContentDialogResult.Primary)
        {
            var address = RoomAddress.Parse(invitation.Text.Trim());
            if (await ConfirmAsync("Join shared file?", "Connect to " + address.Endpoint().GetLeftPart(UriPartial.Authority) + " and replace the current document? Unrelated local edits are not uploaded when joining."))
                await ConnectSharedAsync(address, name.Text);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (!await ConfirmAsync("Upload this design?", "The complete current document, including embedded images and comments, will be sent to " + server.Text.Trim() + ".")) return;
            var address = await RoomTransport.CreateAsync(server.Text.Trim(), key.Password, name.Text.Trim(), DocumentJson.Save(Session.Document));
            key.Password = "";
            await ConnectSharedAsync(address, name.Text);
            await ShowAccessLinkAsync("Save owner access", address.Link(CollaborationApplicationUrl), "This owner link can invite and revoke participants. Save it privately; VectorSpace does not store it after reload. Share a separate guest invitation with collaborators.");
        }
    }
    private async Task ShowConnectedShareAsync()
    {
        if (_collaboration is not { } connection) return;
        var root = new StackPanel { Spacing = 12, Width = 390 };
        root.Children.Add(Studio.Text(Session.Document.Name, 16, Studio.Ink, true));
        root.Children.Add(Wrapped(connection.Transport.Address.Endpoint().GetLeftPart(UriPartial.Authority), 11));
        root.Children.Add(Wrapped(connection.Status + " · " + connection.Role, 12, Studio.Ink));
        var dialog = Dialog("Share file", root, connection.Role == RoomRole.Owner ? "Create invitation" : "Copy access link");
        dialog.SecondaryButtonText = "Leave shared file";
        Func<Task>? selected = null;
        void Action(string label, Func<Task> action)
        {
            var button = new StudioButton(label, () => { selected = action; dialog.Hide(); }) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            root.Children.Add(button);
        }
        Action("People in this file", ShowParticipantsAsync);
        Action("Version history", ShowSharedHistoryAsync);
        Action("Local collaboration recovery", ShowSharedRecoveryAsync);
        Action("Save my access link", () => ShowAccessLinkAsync("Private access link", connection.Transport.Address.Link(CollaborationApplicationUrl), "Anyone with this link receives your invitation's permissions. The link is not an account login."));
        if (connection.Role == RoomRole.Owner) Action("Manage invitations", ShowInvitationsAsync);
        root.Children.Add(Wrapped("Guest permissions are checked on the server. Presence names are self-reported; use one revocable invitation per person when attribution matters.", 10));
        var result = await dialog.ShowAsync();
        if (selected is not null) { await selected(); return; }
        if (result == ContentDialogResult.Primary)
        {
            if (connection.Role == RoomRole.Owner) await CreateInvitationAsync();
            else await ShowAccessLinkAsync("Access link", connection.Transport.Address.Link(CollaborationApplicationUrl), "This link carries your current invitation permissions.");
        }
        else if (result == ContentDialogResult.Secondary) await LeaveSharedAsync();
    }
    private async Task CreateInvitationAsync()
    {
        if (_collaboration is not { } connection) return;
        var root = new StackPanel { Spacing = 12, Width = 360 };
        var label = Studio.Input("Collaborator", "Invitation label"); label.MaxLength = 64; root.Children.Add(label);
        var role = new ComboBox { ItemsSource = new[] { RoomRole.Viewer, RoomRole.Commenter, RoomRole.Editor }, SelectedItem = RoomRole.Editor, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Studio.Font };
        AutomationProperties.SetName(role, "Invitation permission"); root.Children.Add(role);
        root.Children.Add(Wrapped("Can view: inspect and present. Can comment: add/reply/resolve comments. Can edit: change the design. Only your owner invitation manages access."));
        if (await Dialog("Invite to file", root, "Create link", "Cancel").ShowAsync() != ContentDialogResult.Primary) return;
        var invite = await connection.Transport.InviteAsync(label.Text.Trim(), (RoomRole)role.SelectedItem);
        var address = connection.Transport.Address with { Token = invite.Token };
        await ShowAccessLinkAsync("Invitation created", address.Link(CollaborationApplicationUrl), invite.Role + " access · Revoke this invitation at any time from Manage invitations.");
    }
    private async Task ShowAccessLinkAsync(string title, string link, string description)
    {
        var root = new StackPanel { Width = 390, Spacing = 12 };
        root.Children.Add(Wrapped(description, 12, Studio.Ink));
        var box = Studio.Input(link, "Private collaboration link"); box.IsReadOnly = true; box.TextWrapping = TextWrapping.Wrap; box.Height = 96; box.Tag = "Sensitive"; root.Children.Add(box);
        if (await Dialog(title, root, "Copy link").ShowAsync() == ContentDialogResult.Primary)
        {
            var package = new DataPackage(); package.SetText(link); Clipboard.SetContent(package); ShowStatus("Access link copied");
        }
    }
    private async Task ShowInvitationsAsync()
    {
        if (_collaboration is not { } connection) return;
        var grants = await connection.Transport.InvitationsAsync(); var root = new StackPanel { Spacing = 9, Width = 390 };
        var dialog = Dialog("Manage invitations", Studio.Scroll(root)); string? revoke = null;
        foreach (var grant in grants)
        {
            var button = new StudioButton("Revoke", () => { revoke = grant.Id; dialog.Hide(); }) { IsEnabled = grant.Role != RoomRole.Owner };
            AutomationProperties.SetName(button, "Revoke " + grant.Label);
            root.Children.Add(Studio.Columns((Wrapped(grant.Label + " · " + grant.Role, 12, Studio.Ink), -1), (button, 75)));
        }
        await dialog.ShowAsync();
        if (revoke is not null && await ConfirmAsync("Revoke invitation?", "Anyone using this invitation will lose access. Previously downloaded local copies cannot be revoked."))
        { await connection.Transport.RevokeAsync(revoke); ShowStatus("Invitation revoked"); }
    }
}
