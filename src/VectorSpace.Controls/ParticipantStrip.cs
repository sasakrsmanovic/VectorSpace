using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace VectorSpace.Controls;

public sealed record ParticipantIdentity(string Id, string Name, string Color);

/// <summary>Compact, keyboard-focusable participant avatars. No network dependency or global state.</summary>
public sealed class ParticipantStrip : StackPanel
{
    private string _signature = "";
    public event Action<string>? ParticipantInvoked;
    public ParticipantStrip()
    {
        Orientation = Orientation.Horizontal; Spacing = -5; VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(this, "File participants");
    }
    public void Update(IReadOnlyList<ParticipantIdentity> participants, string? following = null)
    {
        var signature = following + "|" + string.Join('|', participants.Select(p => p.Id + ":" + p.Name + ":" + p.Color));
        if (_signature == signature) return; _signature = signature; Children.Clear();
        foreach (var participant in participants.Take(4))
        {
            var initials = string.Concat(participant.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpperInvariant();
            var button = new StudioButton(initials, () => ParticipantInvoked?.Invoke(participant.Id))
            {
                Width = 28, Height = 28, Padding = new(0), CornerRadius = new(14), FontSize = 10,
                RestBackground = participant.Color, Background = Studio.Brush(participant.Color), Foreground = Studio.Brush("#FFFFFF"),
                BorderBrush = Studio.Brush(following == participant.Id ? "#1E1E1E" : "#FFFFFF"), BorderThickness = new(following == participant.Id ? 2 : 1)
            };
            var label = (following == participant.Id ? "Following " : "Follow ") + participant.Name;
            AutomationProperties.SetName(button, label); ToolTipService.SetToolTip(button, label); Children.Add(button);
        }
        if (participants.Count > 4)
        {
            var more = new StudioButton("+" + (participants.Count - 4), () => ParticipantInvoked?.Invoke("")) { Width = 28, Height = 28, Padding = new(0), FontSize = 10, CornerRadius = new(14) };
            AutomationProperties.SetName(more, "All participants"); Children.Add(more);
        }
    }
}
