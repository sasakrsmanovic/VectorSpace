using VectorSpace.Core;

namespace VectorSpace.Documents;

internal static class AppearanceValidation
{
    public static void Validate(DesignNode node, ref long imageCharacters)
    {
        CheckFills(node.Fills, ref imageCharacters);
        CheckEffects(node.Shadows);
        foreach (var entry in node.Overrides.Values)
        {
            if (entry is null) throw new InvalidDataException("Invalid instance override.");
            if (entry.Fills is { } fills) CheckFills(fills, ref imageCharacters);
            if (entry.Effects is { } effects) CheckEffects(effects);
        }
    }
    private static bool Finite(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private static void CheckFills(List<FillStyle> fills, ref long characters)
    {
        if (fills.Count > 64) throw new InvalidDataException("A layer is limited to 64 fills.");
        foreach (var f in fills)
        {
            if (f is null || !Enum.IsDefined(f.Kind) || !Enum.IsDefined(f.Blend) || !Enum.IsDefined(f.ImageMode) || !Enum.IsDefined(f.Spread) ||
                !Finite(f.Opacity, 0, 1) || !f.Start.IsFinite || !f.End.IsFinite || f.Stops is null || f.Stops.Count > 256 ||
                !Finite(f.ImageScale, .001, 1000) || !f.ImageOffset.IsFinite || Math.Abs(f.ImageOffset.X) > 1000 || Math.Abs(f.ImageOffset.Y) > 1000 ||
                !Finite(f.ImageRotation, -360000, 360000) || !Finite(f.Exposure, -1, 1) || !Finite(f.Contrast, -1, 1) || !Finite(f.Saturation, -1, 1))
                throw new InvalidDataException("Invalid paint settings.");
            foreach (var stop in f.Stops)
                if (stop is null || !Finite(stop.Offset, 0, 1) || !Finite(stop.Opacity, 0, 1) || stop.Color is null) throw new InvalidDataException("Invalid gradient stop.");
            var m = f.GradientTransform;
            if (!new[] { m.M11, m.M12, m.M21, m.M22, m.DX, m.DY }.All(v => Finite(v, -1e9, 1e9)) || !m.TryInvert(out _) ||
                f.GradientRadius is { } radius && !Finite(radius, .000001, 1e9) || f.GradientFocal is { IsFinite: false })
                throw new InvalidDataException("Invalid gradient transform.");
            if (f.ImageData is not null)
            {
                characters += f.ImageData.Length;
                if (characters > DocumentJson.MaxDocumentCharacters) throw new InvalidDataException("Embedded images exceed the document budget.");
                EmbeddedImage.Validate(f.ImageData);
            }
        }
    }
    private static void CheckEffects(List<ShadowStyle> effects)
    {
        if (effects.Count > 32) throw new InvalidDataException("A layer is limited to 32 effects.");
        foreach (var s in effects)
            if (s is null || !Enum.IsDefined(s.Kind) || !Finite(s.X, -100000, 100000) || !Finite(s.Y, -100000, 100000) ||
                !Finite(s.Blur, 0, 512) || !Finite(s.Spread, -512, 512) || !Finite(s.Opacity, 0, 1) || s.Color is null)
                throw new InvalidDataException("Invalid layer effect.");
    }
}
