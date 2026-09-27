using VectorSpace.Core;

namespace VectorSpace.Documents;

/// <summary>An original editable sample; every illustration, card and label is a scene node.</summary>
public static class SampleDocument
{
    public static DesignDocument Create()
    {
        var document = new DesignDocument { Name = "Aether · Website exploration", Pages = [new() { Name = "01 · Website" }, new() { Name = "02 · Components" }, new() { Name = "03 · Playground" }] };
        document.ColorStyles = new() { ["Aether / Ink"] = "#22212D", ["Aether / Violet"] = "#7755EE", ["Aether / Lavender"] = "#EEE8FF", ["Aether / Surface"] = "#FCFAF7", ["Aether / Mint"] = "#DFF1E8" };
        var desktop = Frame("Desktop · Landing page", 0, 0, 1040, 820, "#FCFAF7");
        document.Pages[0].Nodes.Add(desktop);
        var navigation = Group("Navigation", 48, 28, 944, 44); desktop.Add(navigation);
        navigation.Add(Text("aether", 0, 3, 120, 36, 27, 700));
        navigation.Add(Text("Product", 326, 14, 74, 20, 13)); navigation.Add(Text("Resources", 424, 14, 90, 20, 13)); navigation.Add(Text("Pricing", 548, 14, 60, 20, 13));
        navigation.Add(Text("Log in", 728, 14, 56, 20, 13)); navigation.Add(Button("Get started", 804, 0, 140, 44, "#22212D"));
        var hero = Group("Hero", 48, 118, 944, 414); desktop.Add(hero);
        var badge = Rectangle("Announcement", 0, 0, 256, 30, "#EEE8FF", 15); hero.Add(badge); badge.Add(Spark("Announcement icon", 13, 9, 10, "#7156B8")); badge.Add(Text("Your ideas deserve a little space", 31, 7, 220, 18, 11, 500, "#7156B8"));
        hero.Add(Text("Big ideas.\nBeautifully\nsimple.", 0, 54, 510, 208, 62, 700));
        hero.Add(Text("A calmer place to think, plan, and make things\nhappen. Bring your next big idea to life, together.", 2, 280, 472, 56, 15, 400, "#777380"));
        var cta = Button("Start creating  →", 0, 357, 178, 48, "#7755EE"); cta.Name = "Primary button"; hero.Add(cta);
        hero.Add(Text("Explore the possibilities ↗", 205, 373, 230, 22, 13, 500));
        var art = Group("A little room for possibility", 544, 12, 400, 400); hero.Add(art);
        var blob = Ellipse("Lavender orbit", 10, 10, 360, 360, "#E8DFFC"); art.Add(blob);
        var star = new DesignNode { Kind = NodeKind.Star, Name = "Spark", X = 320, Y = 22, Width = 54, Height = 54, Sides = 4, StarRatio = .26, Rotation = 12, Fill = "#B59AE8" }; art.Add(star);
        var card = Frame("Project card", 24, 76, 332, 252, "#FFFFFF"); card.Rotation = -7; card.CornerRadius = 14; card.Shadows.Add(new() { Opacity = .11, Blur = 34, Y = 12, Color = "#4C3270" }); art.Add(card);
        card.Add(Text("YOUR SPACE, YOUR PACE", 22, 21, 260, 20, 9, 600, "#91879F"));
        card.Add(Text("Something good\nis taking shape.", 22, 51, 292, 74, 25, 600));
        card.Add(Rectangle("Progress track", 22, 144, 286, 5, "#F0EBF7", 3)); card.Add(Rectangle("Progress", 22, 144, 188, 5, "#AB8ED9", 3));
        for (var i = 0; i < 3; i++)
        {
            var task = Group("Checklist item " + (i + 1), 22, 169 + i * 22, 286, 19); card.Add(task);
            task.Add(Rectangle("Checkbox", 0, 0, 13, 13, i == 2 ? "#EEE8FF" : "#DFF1E8", 4));
            task.Add(Text(new[] { "Make room for a new idea", "Connect the little details", "Create something that matters" }[i], 24, -1, 250, 20, 10, 400, "#807886"));
        }
        var note = Frame("Sticky note", 222, 292, 154, 92, "#F5E9B9"); note.Rotation = 9; note.CornerRadius = 8; note.Shadows.Add(new() { Opacity = .08, Blur = 16, Y = 7 }); note.Add(Text("A little progress,\nevery day.", 17, 18, 128, 55, 15, 500, "#6A5834")); art.Add(note);
        var dot = Ellipse("Mint orbit", 0, 304, 70, 70, "#C7E3D3"); art.Add(dot);
        var benefits = Group("Made for the way you work", 48, 600, 944, 166); desktop.Add(benefits);
        var headings = new[] { "Room to think", "Made to connect", "A little more you" };
        var descriptions = new[] { "From the first spark to the final detail.\nKeep your ideas beautifully organized.", "Good things happen together.\nBuild on each other's best thinking.", "A workspace that feels like yours.\nSimple, flexible, wonderfully personal." };
        var tints = new[] { "#EEE8FF", "#E2EEE5", "#F2E8D8" };
        for (var i = 0; i < 3; i++)
        {
            var feature = Frame(headings[i], i * 322, 0, 300, 166, "#F5F2EC"); feature.CornerRadius = 12; benefits.Add(feature);
            feature.Add(Rectangle("Icon tile", 20, 18, 32, 32, tints[i], 8));
            var mark = new DesignNode { Kind = i == 0 ? NodeKind.Star : i == 1 ? NodeKind.Ellipse : NodeKind.Polygon, Name = "Feature icon", X = 28, Y = 26, Width = 16, Height = 16, Sides = i == 0 ? 4 : 3, StarRatio = .35, Fills = [], Strokes = [new() { Color = "#806B95", Width = 1.5 }] }; feature.Add(mark);
            feature.Add(Text(headings[i], 20, 65, 260, 25, 16, 600)); feature.Add(Text(descriptions[i], 20, 103, 262, 44, 11, 400, "#8A828D"));
        }
        var phone = Frame("Mobile · Landing page", 1120, 0, 360, 820, "#FCFAF7"); document.Pages[0].Nodes.Add(phone);
        phone.Add(Text("aether", 24, 27, 150, 34, 26, 700)); for (var line = 0; line < 3; line++) phone.Add(Rectangle("Menu line", 306, 37 + line * 6, 18, 2, "#22212D", 1));
        phone.Add(Rectangle("Announcement", 24, 107, 241, 30, "#EEE8FF", 15)); phone.Add(Spark("Announcement icon", 37, 117, 10, "#7156B8")); phone.Add(Text("A little room for possibility", 55, 115, 205, 20, 11, 500, "#7156B8"));
        phone.Add(Text("Big ideas.\nBeautifully\nsimple.", 24, 166, 320, 194, 46, 700));
        phone.Add(Text("A calmer place to think, plan,\nand make things happen.", 26, 355, 306, 50, 14, 400, "#777380"));
        var phoneCta = Button("Start creating  →", 24, 430, 312, 48, "#7755EE"); phone.Add(phoneCta);
        var mobileArt = DocumentJson.CloneNode(art, true); mobileArt.X = 23; mobileArt.Y = 518; mobileArt.Width = 314; mobileArt.Height = 300;
        foreach (var n in mobileArt.DescendantsAndSelf().Skip(1)) { n.X *= .76; n.Y *= .76; n.Width *= .76; n.Height *= .76; n.FontSize *= .76; } phone.Add(mobileArt);
        cta.PrototypeTargetId = phone.Id; phoneCta.PrototypeTargetId = desktop.Id;
        var components = document.Pages[1];
        var button = Button("Start creating  →", 0, 0, 178, 48, "#7755EE"); button.Kind = NodeKind.Component; button.Name = "Button / Primary"; components.Nodes.Add(button);
        var secondary = Button("Learn more", 230, 0, 178, 48, "#22212D"); secondary.Kind = NodeKind.Component; secondary.Name = "Button / Secondary"; components.Nodes.Add(secondary);
        var componentCard = DocumentJson.CloneNode(card, true); componentCard.Kind = NodeKind.Component; componentCard.Name = "Card / Project"; componentCard.X = 0; componentCard.Y = 112; componentCard.Rotation = 0; components.Nodes.Add(componentCard);
        document.RebuildParents();
        foreach (var n in document.AllNodes()) if (n.Children.Count > 0) n.Expanded = n.Parent is null;
        return document;
    }
    private static DesignNode Spark(string name, double x, double y, double size, string color) => new() { Kind = NodeKind.Star, Name = name, X = x, Y = y, Width = size, Height = size, Sides = 4, StarRatio = .3, Fill = color };
    private static DesignNode Frame(string name, double x, double y, double w, double h, string fill) => new() { Name = name, Kind = NodeKind.Frame, X = x, Y = y, Width = w, Height = h, Fill = fill, ClipContent = true };
    private static DesignNode Group(string name, double x, double y, double w, double h) => new() { Name = name, Kind = NodeKind.Group, X = x, Y = y, Width = w, Height = h, Fills = [] };
    private static DesignNode Rectangle(string name, double x, double y, double w, double h, string fill, double radius = 0) => new() { Name = name, X = x, Y = y, Width = w, Height = h, Fill = fill, CornerRadius = radius };
    private static DesignNode Ellipse(string name, double x, double y, double w, double h, string fill) => new() { Name = name, Kind = NodeKind.Ellipse, X = x, Y = y, Width = w, Height = h, Fill = fill };
    private static DesignNode Text(string text, double x, double y, double w, double h, double size, int weight = 400, string color = "#22212D") => new() { Name = text.Replace('\n', ' '), Kind = NodeKind.Text, Text = text, X = x, Y = y, Width = w, Height = h, FontSize = size, FontWeight = weight, LineHeight = 1.08, Fill = color };
    private static DesignNode Button(string text, double x, double y, double w, double h, string color)
    {
        var node = Frame(text, x, y, w, h, color); node.CornerRadius = 8;
        var label = Text(text, 12, (h - 16) / 2 - 1, w - 24, 22, 13, 500, "#FFFFFF"); label.TextAlign = TextAlignment.Center; label.HorizontalConstraint = AxisConstraint.Stretch; label.VerticalConstraint = AxisConstraint.Center; node.Add(label); return node;
    }
}
