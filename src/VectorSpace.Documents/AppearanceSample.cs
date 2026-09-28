using VectorSpace.Core;

namespace VectorSpace.Documents;

/// <summary>Original editable paint laboratory. The embedded geometric study contains no external asset or font.</summary>
public static class AppearanceSample
{
    private const string Pattern = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAPAAAACgCAIAAAC9uXYyAAAEAUlEQVR42u3dzY0UMRAG0A2JCMiDA0diQARCBCRCCIgU4MANCRLgBFoJmPHY7u76eVJdR59UfqOd7XbZT99+fJ+uT1+/TJdcuUfkPmm0XKA1Wi7QFlgu0HLlAi0XaI2WC7RGywXaAssFWq5coOUCrdFygdZouUDLlQu0XLlAywVao+UCrdFygZYrF2i5cv8LWrPkVsoFWi7Qv+vFm3dNCiyggQYaaKDHFvjnh9dzBTTQUUBPI57GDTTQ+xd4u+Nx2UADvXOBT6B8mzXQQG9Y4JMd35ANNNCrC3y55uemgQZ6foGDUH5eQAM9s8ABKa+zBrop6OCap00D3RF0Cs1zpoHuBToR5TnWQDcCnVTzQ6aB7gI6teZx00C3AF1A86BpoOuDLqN5xDTQxUEX03zX9GWgVz7cB3T8HXOXb/yoMPUNNNBAtwNdWPMN00DXBF1ec7S91EAfCLqJ5n+aBhpooIEODLqV5r9NAw000EBHBd1Qc5B5RKD3g26rOcI8ItBAAw000EA3Ad1c8x/TQAMNNNDBQKN8+ZkeQAMN9KWg33/+OFJAAx0a9KDjg2SPNBriXaaLg56mvJE10EBHobyFNdBpQIcdkt2uecX0yMsCglfucyk+9X2Q5mnTdxuN70bT1UAfqnnONNBAx9U8YRpooENrftQ00EBH1/yQaaCBTqB53DTQQAOtuoK+UPOgaaCBBlq1BH255hHTQAMNtAIaaKBTgw6i+a5poIEGWgENNNBAAw000EADDTTQQAMNdKN7CrOANiRr6htooIEGGmiggQYa6P2gmXa2HdBAlzsKLMVuO6CB7giaaSf4Aw10udNH488UugXLLVhAA934fOjg53IADXRf0O76Bjr62XYTjW6+tw7o0KePAg10qfOh5xrdeesz0KFP8Aca6FJ3rEw3uu1kCtChb8FaaXTPOSug099T2NP0EXOBNe8pDHWT7OIiNdxVt+Kq4D2F0e76Xm90tz2iQG+WvTd0y5/RVjuegQ5du34X9tm/D3QL0DVMH/2PHdCZQGc3fcKTCqCTgc5r+pxHb0DnA52O9ZnPkoHOCjqL6ZNfjgCdGHR80+e/7QM6N+iwrK96fQ10aNCvXr5VGwtooIEGGmiggQYaaKAV0EADDTTQQAMNNNBAA30h6MhDsnFq5e0Xgnsr8dQ30ApooIEGGmiggQYaaKAV0EAroIEGGmiggQYaaKCBVkADrYAGGmiggQYaaKCBBloBDTTQR4G+6rinXLkIAg20AhpooBfvKfRlMCRbauobaKCB9nNFbtTzoTVaLtAWWC7QcuUCLRdojZYLtEbLBVquXKDlygVaLtAaLRdojZYLtFy5QMuVC7RcoDVaLtAaLRdouXL35P4CwSYsBoDC/6YAAAAASUVORK5CYII=";
    public static DesignDocument Create()
    {
        var frame = new DesignNode { Id = "appearance-gallery", Name = "Aether / Material studies", Kind = NodeKind.Frame,
            Width = 1060, Height = 800, Fill = "#F4F5F7", ClipContent = true };
        frame.Add(Label("AETHER  /  MATERIAL STUDIES", 32, 28, 13, "#657187"));
        frame.Add(Label("A little more depth.", 32, 65, 46, "#152438", 700));
        frame.Add(Label("Editable images, retained gradients and layered effects. Select a study to make it yours.", 34, 132, 15, "#657187"));
        var modes = Enum.GetValues<ImageScaleMode>();
        for (var i = 0; i < modes.Length; i++)
        {
            var x = 32 + i * 254; var mode = modes[i];
            frame.Add(Label("0" + (i + 1) + "  /  " + mode.ToString().ToUpperInvariant(), x, 193, 12, "#657187", 650));
            var n = frame.Add(new() { Id = "study-" + mode, Name = mode + " image study", X = x, Y = 224, Width = 230, Height = 200, CornerRadius = 16,
                Fills = [new() { Color = "#E3E8EB" }, new() { Kind = FillKind.Image, ImageData = Pattern, ImageMode = mode,
                    ImageScale = mode == ImageScaleMode.Tile ? .45 : mode == ImageScaleMode.Crop ? 1.5 : 1,
                    ImageOffset = mode == ImageScaleMode.Crop ? new(.12, 0) : Vec2.Zero }],
                Shadows = [new() { X = 0, Y = 7, Blur = 18, Opacity = .1 }] });
        }
        var titles = new[] { "A soft spectrum", "Two independent shadows", "Light from within" };
        for (var i = 0; i < titles.Length; i++)
        {
            var x = 32 + i * 340; frame.Add(Label(titles[i], x, 477, 15, "#152438", 650));
            var n = frame.Add(new() { Id = "material-" + i, Name = titles[i], X = x, Y = 517, Width = 310, Height = 174, CornerRadius = 24,
                Fills = [new() { Kind = FillKind.LinearGradient, Start = new(0, 0), End = new(1, 1),
                    Stops = [new() { Color = "#C7F0DE" }, new() { Offset = .6, Color = "#80BDE3" }, new() { Offset = 1, Color = "#AC8BCB" }] }] });
            if (i == 1) n.Shadows = [new() { X = -10, Y = 8, Blur = 18, Color = "#028E83", Opacity = .35 }, new() { X = 12, Y = 16, Blur = 24, Color = "#925B9E", Opacity = .35 }];
            if (i == 2) n.Shadows = [new() { Kind = EffectKind.InnerShadow, X = 8, Y = 10, Blur = 22, Spread = 2, Color = "#25405D", Opacity = .55 }];
        }
        frame.Add(Label("Try Edit image crop: drag to reposition, wheel to zoom, Enter to finish.", 34, 739, 13, "#657187"));
        var doc = new DesignDocument { Name = "Aether / Appearance playground", Pages = [new() { Name = "Material studies", Nodes = [frame] }] };
        doc.RebuildParents(); DocumentJson.Validate(doc); return doc;
    }
    private static DesignNode Label(string text, double x, double y, double size, string color, int weight = 400) =>
        new() { Name = text, Kind = NodeKind.Text, Text = text, X = x, Y = y, Width = text.Length * size * .62, Height = size * 1.3, FontSize = size, FontWeight = weight, Fill = color };
}
