// Morph change: the Futuristic theme style. Deep navy surfaces, a neon accent and cards outlined by a soft glow of the
// accent. It fills the same resource keys as Material You (MaterialYouPalette is the container), so every control the
// two styles share reads it without new templates. Light and dark both exist: "match my computer" still works.

using Avalonia.Media;

namespace Filee.App.Services;

/// <summary>Builds the Futuristic palette for one variant.</summary>
public static class FuturisticPalette
{
    /// <summary>The signature accent when the user hasn't chosen a colour of their own: neon cyan.</summary>
    public const string SignatureAccent = "#22D3EE";

    /// <summary>
    /// The palette for <paramref name="seed"/>. <paramref name="useSignature"/> replaces the seed with the signature cyan
    /// (the default accent, which belongs to the Soft look). A monochrome seed keeps the look colourless.
    /// </summary>
    public static MaterialYouPalette Generate(Color seed, bool isDark, bool useSignature)
    {
        var monochrome = !useSignature && MaterialYouPalette.IsMonochrome(seed);
        var source = useSignature ? Color.Parse(SignatureAccent) : seed;
        var (h, s, l) = MaterialYouPalette.RgbToHsl(source);

        return isDark ? Dark(source, h, s, monochrome, useSignature) : Light(source, h, s, l, monochrome);
    }

    private static MaterialYouPalette Dark(Color source, double h, double s, bool monochrome, bool signature)
    {
        var page = Color.Parse("#060A13");
        var surface = Color.Parse("#0B1222");
        var surfaceAlt = Color.Parse("#121C30");

        // Neon: saturated and bright enough to glow on near-black, whatever the chosen colour's own lightness.
        var accent = monochrome ? Color.Parse("#E6F1FF")
            : signature ? source
            : MaterialYouPalette.HslToRgb(h, Math.Clamp(Math.Max(s, 0.80), 0, 0.95), 0.62);
        var onAccent = SoftPalette.OnAccent(accent);
        var faint = Mix(surface, accent, 0.10);

        return new MaterialYouPalette
        {
            Page = page,
            Surface = surface,
            SurfaceAlt = surfaceAlt,
            SubtleBorder = WithAlpha(accent, 0x55),
            TextPrimary = Color.Parse("#E8F1FF"),
            TextSecondary = Color.Parse("#9AAECB"),
            Danger = Color.Parse("#FF6B86"),
            Success = Color.Parse("#3EF0B0"),
            Warning = Color.Parse("#FFC24D"),
            CardShadow = Glow(accent, ring: 0x1F, glow: 0x2E, blur: 22, drop: 0),

            Accent = accent,
            OnAccent = onAccent,
            AccentSoft = Mix(surface, accent, 0.24),
            AccentFaint = faint,
            AccentStrong = monochrome ? Colors.White : Readable(h, Math.Min(Math.Max(s, 0.70), 0.95), 0.80, faint, step: 0.01),
        };
    }

    private static MaterialYouPalette Light(Color source, double h, double s, double l, bool monochrome)
    {
        var surface = Colors.White;
        Color accent, onAccent;
        if (monochrome)
        {
            accent = Color.Parse("#0A1628");
            onAccent = Colors.White;
        }
        else
        {
            // Vivid, then only as dark as readable text on it needs (the same rule as Material You's light accent).
            var sat = Math.Clamp(Math.Max(s, 0.75), 0, 0.95);
            var vivid = MaterialYouPalette.HslToRgb(h, sat, Math.Min(l, 0.50));
            (accent, onAccent) = MaterialYouPalette.LightPrimary(vivid, h, sat, Math.Min(l, 0.50));
        }

        var faint = Mix(surface, accent, 0.07);

        return new MaterialYouPalette
        {
            Page = Color.Parse("#F1F5FB"),
            Surface = surface,
            SurfaceAlt = Color.Parse("#E6EEF8"),
            SubtleBorder = WithAlpha(accent, 0x40),
            TextPrimary = Color.Parse("#0A1628"),
            TextSecondary = Color.Parse("#43587A"),
            Danger = Color.Parse("#C8102E"),
            Success = Color.Parse("#04795B"),
            Warning = Color.Parse("#8A5A00"),
            CardShadow = Glow(accent, ring: 0x14, glow: 0x22, blur: 20, drop: 4),

            Accent = accent,
            OnAccent = onAccent,
            AccentSoft = Mix(surface, accent, 0.16),
            AccentFaint = faint,
            AccentStrong = monochrome ? accent : Readable(h, Math.Min(Math.Max(s, 0.60), 0.90), 0.26, faint, step: -0.01),
        };
    }

    /// <summary>
    /// The accent's hue at <paramref name="lightness"/>, moved by <paramref name="step"/> (lighter or darker) until text in it
    /// reads on <paramref name="background"/>: some hues (yellow) are bright even when dark.
    /// </summary>
    private static Color Readable(double h, double s, double lightness, Color background, double step)
    {
        var color = MaterialYouPalette.HslToRgb(h, s, lightness);
        while (MaterialYouPalette.ContrastRatio(color, background) < 4.5 && lightness is > 0.02 and < 0.98)
        {
            lightness += step;
            color = MaterialYouPalette.HslToRgb(h, s, lightness);
        }
        return color;
    }

    /// <summary>A hairline ring and a soft halo of the accent around each card.</summary>
    private static BoxShadows Glow(Color accent, byte ring, byte glow, int blur, int drop) =>
        BoxShadows.Parse($"0 0 0 1 {Hex(WithAlpha(accent, ring))}, 0 {drop} {blur} 0 {Hex(WithAlpha(accent, glow))}");

    internal static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
