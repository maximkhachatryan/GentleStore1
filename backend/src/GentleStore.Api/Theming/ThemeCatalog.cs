using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;

namespace GentleStore.Api.Theming;

/// <summary>
/// Everything a store's theme can be made of: the presets, the fonts and the corner radii. A
/// stored theme names these by key and is checked against them on the way in, so whatever ends up
/// in a page's CSS comes from this file or is a validated "#rrggbb" — never text someone typed.
/// </summary>
public static partial class ThemeCatalog
{
    private const string SystemSans = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
    private const string SystemSerif = "Georgia, 'Times New Roman', serif";

    /// <summary>
    /// The frontends load these by key (packages/shared/src/fonts.ts); keep the two lists in
    /// step. Each stack ends in an Armenian face of the same kind, so Armenian text keeps the style
    /// even where the display face has no Armenian letters.
    /// </summary>
    public static readonly IReadOnlyList<ThemeFontDto> Fonts =
    [
        new("system", "System", SystemSans, Serif: false),
        new("inter", "Inter", $"'Inter Variable', 'Noto Sans Armenian Variable', {SystemSans}", Serif: false),
        new("montserrat", "Montserrat", $"'Montserrat Variable', 'Noto Sans Armenian Variable', {SystemSans}", Serif: false),
        new("roboto", "Roboto", $"'Roboto Variable', 'Noto Sans Armenian Variable', {SystemSans}", Serif: false),
        new("noto-sans", "Noto Sans", $"'Noto Sans Variable', 'Noto Sans Armenian Variable', {SystemSans}", Serif: false),
        new("playfair", "Playfair Display", $"'Playfair Display Variable', 'Noto Serif Armenian Variable', {SystemSerif}", Serif: true),
        new("lora", "Lora", $"'Lora Variable', 'Noto Serif Armenian Variable', {SystemSerif}", Serif: true),
        new("noto-serif", "Noto Serif", $"'Noto Serif Variable', 'Noto Serif Armenian Variable', {SystemSerif}", Serif: true),
    ];

    public static readonly IReadOnlyList<ThemeRadiusDto> Radii =
    [
        new("square", 0.25m),
        new("soft", 0.6m),
        new("round", 1m),
        new("extra-round", 1.6m),
    ];

    /// <summary>"classic" is the platform's own look, so choosing it changes nothing but the name.</summary>
    public static readonly IReadOnlyList<ThemePresetDto> Presets =
    [
        new("classic", "#059669", "#f8fafc", "system", "system", "round", ShowStoreName: true),
        new("minimal", "#111827", "#ffffff", "inter", "inter", "square", ShowStoreName: true),
        new("boutique", "#9d4b6e", "#fbf7f4", "playfair", "inter", "soft", ShowStoreName: true),
        new("bold", "#e4572e", "#fafafa", "montserrat", "inter", "extra-round", ShowStoreName: true),
        new("natural", "#4d7c0f", "#faf8f2", "lora", "noto-sans", "round", ShowStoreName: true),
    ];

    public static ThemeCatalogDto Catalog { get; } = new(Presets, Fonts, Radii);

    public const int WebsiteUrlMaxLength = 300;

    /// <summary>
    /// Checks a theme from the console and turns it into what is stored: trimmed, colours in
    /// lowercase, blanks as nulls.
    /// </summary>
    /// <returns>The theme, or an error code and message for the console.</returns>
    public static (StoreTheme? Theme, string? Code, string? Error) Validate(StoreThemeRequest req)
    {
        var preset = req.Preset?.Trim().ToLowerInvariant() ?? "";
        if (Presets.All(p => p.Key != preset)) return (null, "theme_preset_unknown", "Unknown preset.");

        string? brand = null, background = null;
        if (!TryColor(req.BrandColor, ref brand)) return (null, "theme_color_invalid", "Brand colour must look like #1a2b3c.");
        if (!TryColor(req.BackgroundColor, ref background)) return (null, "theme_color_invalid", "Background colour must look like #1a2b3c.");

        var heading = Blank(req.HeadingFont);
        var body = Blank(req.BodyFont);
        if ((heading is not null && FindFont(heading) is null) || (body is not null && FindFont(body) is null))
            return (null, "theme_font_unknown", "Unknown font.");

        var radius = Blank(req.Radius);
        if (radius is not null && Radii.All(r => r.Key != radius))
            return (null, "theme_radius_unknown", "Unknown corner style.");

        var website = Blank(req.WebsiteUrl);
        if (website is not null)
        {
            if (website.Length > WebsiteUrlMaxLength
                || !Uri.TryCreate(website, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrEmpty(uri.Host))
                return (null, "theme_website_invalid", "Website must be a full https:// address.");
            website = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        return (new StoreTheme
        {
            Preset = preset,
            BrandColor = brand,
            BackgroundColor = background,
            HeadingFont = heading,
            BodyFont = body,
            Radius = radius,
            ShowStoreName = req.ShowStoreName,
            WebsiteUrl = website
        }, null, null);
    }

    public static StoreThemeRequest ToRequest(StoreTheme t) =>
        new(t.Preset, t.BrandColor, t.BackgroundColor, t.HeadingFont, t.BodyFont, t.Radius, t.ShowStoreName, t.WebsiteUrl);

    /// <summary>The theme with its preset filled in. Null for a store without one.</summary>
    public static StoreThemeDto? Resolve(StoreTheme? theme)
    {
        if (theme is null) return null;

        // Stored values were valid when saved; one the catalog has since dropped falls back to the preset.
        var preset = Presets.FirstOrDefault(p => p.Key == theme.Preset) ?? Presets[0];
        var brand = theme.BrandColor is { } b && IsColor(b) ? b : preset.BrandColor;
        var background = theme.BackgroundColor is { } bg && IsColor(bg) ? bg : preset.BackgroundColor;
        var heading = FindFont(theme.HeadingFont) ?? FindFont(preset.HeadingFont)!;
        var body = FindFont(theme.BodyFont) ?? FindFont(preset.BodyFont)!;
        var radius = Radii.FirstOrDefault(r => r.Key == theme.Radius) ?? Radii.First(r => r.Key == preset.Radius);
        var onBrand = OnColor(brand);

        var variables = new Dictionary<string, string>
        {
            ["--gs-brand"] = brand,
            ["--gs-on-brand"] = onBrand,
            ["--gs-bg"] = background,
            ["--gs-font-heading"] = heading.Stack,
            ["--gs-font-body"] = body.Stack,
            ["--gs-radius"] = radius.Scale.ToString(CultureInfo.InvariantCulture),
        };

        var fonts = new[] { heading.Key, body.Key }.Where(k => k != "system").Distinct().ToList();

        return new StoreThemeDto(
            preset.Key, brand, onBrand, background, heading.Key, body.Key, radius.Key,
            theme.ShowStoreName ?? preset.ShowStoreName, theme.WebsiteUrl, variables, fonts);
    }

    /// <summary>The variables as a stylesheet for the page head.</summary>
    public static string RenderStyle(StoreThemeDto theme)
    {
        var css = new StringBuilder(":root{");
        foreach (var (name, value) in theme.CssVariables) css.Append(name).Append(':').Append(value).Append(';');
        return css.Append('}').ToString();
    }

    private static ThemeFontDto? FindFont(string? key) => key is null ? null : Fonts.FirstOrDefault(f => f.Key == key);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryColor(string? input, ref string? color)
    {
        var value = Blank(input);
        if (value is null) return true;
        if (!IsColor(value)) return false;
        color = value.ToLowerInvariant();
        return true;
    }

    private static bool IsColor(string value) => HexColor().IsMatch(value);

    /// <summary>
    /// White or near-black text, whichever reads better on <paramref name="hex"/> (WCAG contrast).
    /// The console previews the same choice (packages/shared/src/theme.ts).
    /// </summary>
    private static string OnColor(string hex)
    {
        const string dark = "#111827";
        var l = Luminance(hex);
        var withWhite = 1.05 / (l + 0.05);
        var withDark = (l + 0.05) / (Luminance(dark) + 0.05);
        return withWhite >= withDark ? "#ffffff" : dark;
    }

    private static double Luminance(string hex)
    {
        static double Channel(string hex, int at)
        {
            var c = int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(hex, 1) + 0.7152 * Channel(hex, 3) + 0.0722 * Channel(hex, 5);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
