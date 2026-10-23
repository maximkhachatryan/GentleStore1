using System.Globalization;
using System.Net;
using GentleStore.Api.Storefront;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.LinkPreviews;

/// <summary>
/// What chat apps see when a storefront link is shared. Their crawlers do not run JavaScript, so
/// the storefront's nginx sends them here (by user agent) for a page of Open Graph tags; people
/// keep getting the app. The image is served back through the storefront's own domain.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/link-preview")]
public class LinkPreviewController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LinkPreviewImages _images;
    private readonly StorefrontOptions _storefront;

    public LinkPreviewController(AppDbContext db, LinkPreviewImages images, IOptions<StorefrontOptions> storefront)
    {
        _db = db;
        _images = images;
        _storefront = storefront.Value;
    }

    /// <param name="path">The storefront path that was shared: "{slug}", "{slug}/product/{id}" or "{slug}/category/{id}".</param>
    [HttpGet("page/{**path}")]
    public async Task<IActionResult> Page(string? path, CancellationToken ct)
    {
        var segments = (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return NotFound();

        var slug = segments[0];
        var store = await _db.Stores.AsNoTracking()
            .Where(s => s.Slug == slug && s.IsActive)
            .Select(s => new { s.Id, s.Name, s.Slug, s.Description, s.LogoUrl, s.Currency })
            .FirstOrDefaultAsync(ct);
        if (store is null) return NotFound();

        var origin = _storefront.Url.TrimEnd('/');
        var storeUrl = $"{origin}/{store.Slug}";

        var preview = new Preview(store.Name, store.Description, storeUrl, store.LogoUrl, "website");

        if (segments.Length >= 3 && Guid.TryParse(segments[2], out var id))
        {
            if (segments[1] == "product")
            {
                var product = await _db.Products.AsNoTracking()
                    .Where(p => p.Id == id && p.StoreId == store.Id && p.IsAvailable)
                    .Select(p => new
                    {
                        p.Name,
                        p.Description,
                        p.Price,
                        Image = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(ct);

                if (product is not null)
                {
                    var price = product.Price is { } amount
                        ? $"{amount.ToString("#,0.##", CultureInfo.InvariantCulture)} {store.Currency}"
                        : null;
                    var description = string.Join(" · ", new[] { price, product.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));

                    preview = new Preview(product.Name, description, $"{storeUrl}/product/{id}",
                        product.Image ?? store.LogoUrl, "product");
                }
            }
            else if (segments[1] == "category")
            {
                var category = await _db.Categories.AsNoTracking()
                    .Where(c => c.Id == id && c.StoreId == store.Id)
                    .Select(c => new
                    {
                        c.Name,
                        // A category has no picture of its own; its first product's stands in.
                        Image = c.Products
                            .Where(p => p.IsAvailable && p.Images.Any())
                            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                            .Select(p => p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault())
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(ct);

                if (category is not null)
                    preview = new Preview(category.Name, store.Name, $"{storeUrl}/category/{id}",
                        category.Image ?? store.LogoUrl, "website");
            }
        }

        var image = await ResolveImageAsync(preview.ImageUrl, origin, ct);

        Response.Headers.CacheControl = "public, max-age=300";
        return Content(Render(preview, store.Name, image), "text/html; charset=utf-8");
    }

    [HttpGet("image/{fileName}")]
    public async Task<IActionResult> Image(string fileName, CancellationToken ct)
    {
        var image = await _images.EnsureAsync(fileName, ct);
        if (image is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(_images.PreviewPath(fileName), "image/jpeg");
    }

    private sealed record Preview(string Title, string? Description, string Url, string? ImageUrl, string Type);

    private sealed record ResolvedImage(string Url, int? Width, int? Height);

    private async Task<ResolvedImage?> ResolveImageAsync(string? imageUrl, string origin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        if (LinkPreviewImages.UploadFileName(imageUrl) is { } upload)
        {
            // Made now rather than on the crawler's follow-up request, so the page can state the
            // size and the image is ready the moment it is asked for.
            var made = await _images.EnsureAsync(upload, ct);
            return made is null ? null : new ResolvedImage($"{origin}/_preview/image/{upload}", made.Width, made.Height);
        }

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute) &&
               (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            ? new ResolvedImage(imageUrl, null, null)
            : null;
    }

    private static string Render(Preview p, string siteName, ResolvedImage? image)
    {
        var description = Clip(p.Description, 200);
        var tags = new List<string>
        {
            Meta("property", "og:type", p.Type),
            Meta("property", "og:site_name", siteName),
            Meta("property", "og:title", p.Title),
            Meta("property", "og:url", p.Url),
            Meta("name", "twitter:card", image is null ? "summary" : "summary_large_image")
        };

        if (description is not null)
        {
            tags.Add(Meta("name", "description", description));
            tags.Add(Meta("property", "og:description", description));
        }

        if (image is not null)
        {
            tags.Add(Meta("property", "og:image", image.Url));
            if (image.Width is { } w && image.Height is { } h)
            {
                tags.Add(Meta("property", "og:image:type", "image/jpeg"));
                tags.Add(Meta("property", "og:image:width", w.ToString(CultureInfo.InvariantCulture)));
                tags.Add(Meta("property", "og:image:height", h.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return $"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>{E(p.Title)}</title>
            <link rel="canonical" href="{E(p.Url)}">
            {string.Join("\n", tags)}
            </head>
            <body><a href="{E(p.Url)}">{E(p.Title)}</a></body>
            </html>
            """;
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
