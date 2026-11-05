namespace GentleStore.Domain.Entities;

/// <summary>
/// How a store's storefront looks: one of the platform's presets, plus whatever the super admin
/// overrode on top of it. Unset values fall through to the preset (Api/Theming/ThemeCatalog.cs),
/// which also holds the lists the keys below are checked against.
/// </summary>
public class StoreTheme
{
    /// <summary>A preset key, e.g. "classic" or "boutique".</summary>
    public string Preset { get; set; } = "classic";

    /// <summary>"#rrggbb": buttons, links, prices, highlights.</summary>
    public string? BrandColor { get; set; }

    /// <summary>"#rrggbb": the page behind the cards.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>A font key from the catalog.</summary>
    public string? HeadingFont { get; set; }

    public string? BodyFont { get; set; }

    /// <summary>A radius key from the catalog: how rounded cards and buttons are.</summary>
    public string? Radius { get; set; }

    /// <summary>
    /// Whether the header shows the store's name beside its logo. Off suits a logo that already
    /// spells the name out.
    /// </summary>
    public bool? ShowStoreName { get; set; }

    /// <summary>
    /// The store's own website ("https://flowers.am"), when the shop sits beside one, e.g. at
    /// shop.flowers.am. The storefront links back to it.
    /// </summary>
    public string? WebsiteUrl { get; set; }
}
