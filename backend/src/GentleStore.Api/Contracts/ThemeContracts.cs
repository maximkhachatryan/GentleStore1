namespace GentleStore.Api.Contracts;

/// <summary>A store's theme as the super admin sets it: a preset and the values overridden on top.</summary>
public record StoreThemeRequest(
    string Preset, string? BrandColor, string? BackgroundColor, string? HeadingFont, string? BodyFont,
    string? Radius, bool? ShowStoreName, string? WebsiteUrl);

/// <summary>
/// A theme with every value filled in from its preset — what the storefront renders.
/// </summary>
/// <param name="OnBrandColor">Text colour on brand-coloured backgrounds, chosen for contrast.</param>
/// <param name="CssVariables">
/// The --gs-* variables the storefront's stylesheet reads (apps/storefront/src/index.css). The page
/// head and the app both apply exactly these, so the two never disagree.
/// </param>
/// <param name="Fonts">The catalog keys of the fonts to load (heading and body, without duplicates).</param>
public record StoreThemeDto(
    string Preset, string BrandColor, string OnBrandColor, string BackgroundColor,
    string HeadingFont, string BodyFont, string Radius, bool ShowStoreName, string? WebsiteUrl,
    IReadOnlyDictionary<string, string> CssVariables, IReadOnlyList<string> Fonts);

public record ThemePresetDto(
    string Key, string BrandColor, string BackgroundColor, string HeadingFont, string BodyFont, string Radius,
    bool ShowStoreName);

/// <param name="Stack">The CSS font-family list, Armenian fallback included.</param>
public record ThemeFontDto(string Key, string Label, string Stack, bool Serif);

/// <param name="Scale">What Tailwind's radii are multiplied by; 1 is the platform's own.</param>
public record ThemeRadiusDto(string Key, decimal Scale);

public record ThemeCatalogDto(
    IReadOnlyList<ThemePresetDto> Presets, IReadOnlyList<ThemeFontDto> Fonts, IReadOnlyList<ThemeRadiusDto> Radii);

/// <param name="Theme">What is stored; null when the store uses the platform's look.</param>
/// <param name="Resolved">The same with the preset filled in; null when <paramref name="Theme"/> is.</param>
public record StoreThemeSettingsDto(StoreThemeRequest? Theme, StoreThemeDto? Resolved, ThemeCatalogDto Catalog);
