using System.Globalization;
using GentleStore.Api.LinkPreviews;
using GentleStore.Api.Storefront;
using GentleStore.Api.Uploads;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Seo;

public enum StorefrontPageKind
{
    Store,
    Category,
    Product,

    /// <summary>A page below the store that is not catalogue — the cart, orders, an invite link — or no longer exists.</summary>
    Other
}

public sealed record PageStore(string Name, string? Description, string Phone, string Currency, string Url);

public sealed record PageImage(string Url, int? Width, int? Height);

/// <summary>What a product costs: one price, or the spread across its variants.</summary>
public sealed record PageOffer(decimal LowPrice, decimal HighPrice, bool InStock);

/// <summary>A storefront page as search engines and chat apps should see it.</summary>
/// <param name="Title">The page's own name: the store's, the category's or the product's.</param>
/// <param name="Indexable">
/// False for invite-only stores and for <see cref="StorefrontPageKind.Other"/> pages. Those still
/// preview as the store when shared, but search engines are asked to leave them out.
/// </param>
public sealed record StorefrontPage(
    StorefrontPageKind Kind,
    PageStore Store,
    string Title,
    string? Description,
    string Url,
    PageImage? Image,
    PageOffer? Offer,
    bool Indexable);

/// <summary>
/// Resolves a storefront path to what its page is about. The storefront is a client-rendered app,
/// so this is the only description of a page that crawlers which do not run JavaScript ever get.
/// </summary>
public class StorefrontPageMeta
{
    private readonly AppDbContext _db;
    private readonly LinkPreviewImages _images;
    private readonly StorefrontOptions _storefront;

    public StorefrontPageMeta(AppDbContext db, LinkPreviewImages images, IOptions<StorefrontOptions> storefront)
    {
        _db = db;
        _images = images;
        _storefront = storefront.Value;
    }

    /// <param name="slug">The store.</param>
    /// <param name="segments">The path below the store: none, "product/{id}", "category/{id}" or anything else.</param>
    /// <returns>Null when <paramref name="slug"/> names no active store.</returns>
    public async Task<StorefrontPage?> FindAsync(string slug, string[] segments, CancellationToken ct)
    {
        var store = await _db.Stores.AsNoTracking()
            .Where(s => s.Slug == slug && s.IsActive)
            .Select(s => new
            {
                s.Id, s.Name, s.Slug, s.CustomDomain, s.Description, s.LogoUrl, s.Phone, s.Currency, s.StorefrontAccess
            })
            .FirstOrDefaultAsync(ct);
        if (store is null) return null;

        // On the store's own domain when it has one, whichever host this page was loaded from —
        // so the platform's copy of the page names the store's as the one to index.
        var storeUrl = StorefrontLinks.StoreBase(_storefront.Url, store.Slug, store.CustomDomain);
        var isPublic = store.StorefrontAccess == StorefrontAccessMode.Public;

        var kind = segments.Length == 0 ? StorefrontPageKind.Store : StorefrontPageKind.Other;
        var title = store.Name;
        var description = store.Description;
        var url = storeUrl;
        var imageUrl = store.LogoUrl;
        PageOffer? offer = null;

        if (segments.Length == 2 && Guid.TryParse(segments[1], out var id))
        {
            if (segments[0] == "product")
            {
                var product = await _db.Products.AsNoTracking()
                    .Where(p => p.Id == id && p.StoreId == store.Id && p.IsAvailable)
                    .Select(p => new
                    {
                        p.Name,
                        p.Description,
                        p.Price,
                        Variants = p.Variants.Select(v => new { v.Price, v.IsAvailable }).ToList(),
                        Image = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(ct);

                if (product is not null)
                {
                    // Once a product has variants they carry the price and the stock, as on the product page.
                    offer = product.Variants.Count > 0
                        ? new PageOffer(
                            product.Variants.Min(v => v.Price),
                            product.Variants.Max(v => v.Price),
                            product.Variants.Any(v => v.IsAvailable))
                        : product.Price is { } price ? new PageOffer(price, price, InStock: true) : null;

                    var priceText = offer is null ? null : FormatPrice(offer, store.Currency);

                    kind = StorefrontPageKind.Product;
                    title = product.Name;
                    description = string.Join(" · ", new[] { priceText, product.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    url = $"{storeUrl}/product/{id}";
                    imageUrl = product.Image ?? store.LogoUrl;
                }
            }
            else if (segments[0] == "category")
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
                {
                    kind = StorefrontPageKind.Category;
                    title = category.Name;
                    description = store.Name;
                    url = $"{storeUrl}/category/{id}";
                    imageUrl = category.Image ?? store.LogoUrl;
                }
            }
        }

        var image = await ResolveImageAsync(imageUrl, StorefrontLinks.Origin(_storefront.Url, store.CustomDomain), ct);
        var pageStore = new PageStore(store.Name, store.Description, store.Phone, store.Currency, storeUrl);

        return new StorefrontPage(kind, pageStore, title, description, url, image, offer,
            Indexable: isPublic && kind != StorefrontPageKind.Other);
    }

    private static string FormatPrice(PageOffer offer, string currency)
    {
        static string Amount(decimal value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);

        return offer.LowPrice == offer.HighPrice
            ? $"{Amount(offer.LowPrice)} {currency}"
            : $"{Amount(offer.LowPrice)}–{Amount(offer.HighPrice)} {currency}";
    }

    private async Task<PageImage?> ResolveImageAsync(string? imageUrl, string origin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        if (UploadImages.UploadFileName(imageUrl) is { } upload)
        {
            // Made now rather than on the crawler's follow-up request, so the page can state the
            // size and the image is ready the moment it is asked for.
            var made = await _images.EnsureAsync(upload, ct);
            return made is null ? null : new PageImage($"{origin}/_preview/image/{upload}", made.Width, made.Height);
        }

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute) &&
               (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            ? new PageImage(imageUrl, null, null)
            : null;
    }
}
