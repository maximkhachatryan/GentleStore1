using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace GentleStore.Api.Seo;

/// <summary>Renders the &lt;head&gt; tags that describe a storefront page to search engines and chat apps.</summary>
public static class PageHead
{
    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        // Keeps Armenian and Cyrillic readable. HTML-sensitive characters are escaped regardless,
        // which is what makes the JSON safe to drop inside a <script> element.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Render(StorefrontPage page)
    {
        var description = Clip(page.Description, 200);
        var tags = new List<string> { $"<title>{E(DocumentTitle(page))}</title>" };

        if (description is not null) tags.Add(Meta("name", "description", description));

        tags.Add(page.Indexable
            ? $"<link rel=\"canonical\" href=\"{E(page.Url)}\">"
            : Meta("name", "robots", "noindex"));

        tags.Add(Meta("property", "og:type", page.Kind == StorefrontPageKind.Product ? "product" : "website"));
        tags.Add(Meta("property", "og:site_name", page.Store.Name));
        tags.Add(Meta("property", "og:title", page.Title));
        tags.Add(Meta("property", "og:url", page.Url));
        tags.Add(Meta("name", "twitter:card", page.Image is null ? "summary" : "summary_large_image"));
        if (description is not null) tags.Add(Meta("property", "og:description", description));

        if (page.Image is { } image)
        {
            tags.Add(Meta("property", "og:image", image.Url));
            if (image.Width is { } w && image.Height is { } h)
            {
                tags.Add(Meta("property", "og:image:type", "image/jpeg"));
                tags.Add(Meta("property", "og:image:width", w.ToString(CultureInfo.InvariantCulture)));
                tags.Add(Meta("property", "og:image:height", h.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (page.Indexable && StructuredData(page) is { } data)
            tags.Add($"<script type=\"application/ld+json\">{data.ToJsonString(JsonLdOptions)}</script>");

        return string.Join("\n", tags);
    }

    /// <summary>
    /// The tab title. The storefront sets the same one as people navigate inside the app
    /// (apps/storefront/src/lib/pageTitle.ts); keep the two formats in step.
    /// </summary>
    private static string DocumentTitle(StorefrontPage page) =>
        page.Kind is StorefrontPageKind.Product or StorefrontPageKind.Category
            ? $"{page.Title} · {page.Store.Name}"
            : page.Title;

    /// <summary>schema.org data Google reads for rich results: the shop itself, or a product and its price.</summary>
    private static JsonObject? StructuredData(StorefrontPage page)
    {
        switch (page.Kind)
        {
            case StorefrontPageKind.Store:
            {
                var store = new JsonObject
                {
                    ["@context"] = "https://schema.org",
                    ["@type"] = "OnlineStore",
                    ["name"] = page.Store.Name,
                    ["url"] = page.Url,
                    ["telephone"] = page.Store.Phone
                };
                if (!string.IsNullOrWhiteSpace(page.Store.Description)) store["description"] = page.Store.Description;
                if (page.Image is not null) store["logo"] = page.Image.Url;
                return store;
            }

            case StorefrontPageKind.Product:
            {
                var product = new JsonObject
                {
                    ["@context"] = "https://schema.org",
                    ["@type"] = "Product",
                    ["name"] = page.Title,
                    ["url"] = page.Url
                };
                if (page.Image is not null) product["image"] = page.Image.Url;

                // "Price on request" products have no offer to state; Google then shows a plain result.
                if (page.Offer is { } offer)
                {
                    var availability = offer.InStock ? "https://schema.org/InStock" : "https://schema.org/OutOfStock";
                    product["offers"] = offer.LowPrice == offer.HighPrice
                        ? new JsonObject
                        {
                            ["@type"] = "Offer",
                            ["url"] = page.Url,
                            ["price"] = offer.LowPrice,
                            ["priceCurrency"] = page.Store.Currency,
                            ["availability"] = availability
                        }
                        : new JsonObject
                        {
                            ["@type"] = "AggregateOffer",
                            ["url"] = page.Url,
                            ["lowPrice"] = offer.LowPrice,
                            ["highPrice"] = offer.HighPrice,
                            ["priceCurrency"] = page.Store.Currency,
                            ["availability"] = availability
                        };
                }

                return product;
            }

            default:
                return null;
        }
    }

    private static string Meta(string attribute, string key, string value) =>
        $"<meta {attribute}=\"{key}\" content=\"{E(value)}\">";

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var flat = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= max ? flat : flat[..(max - 1)] + "…";
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
