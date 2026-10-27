using System.Text;
using System.Xml;
using GentleStore.Api.Domains;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Seo;

/// <summary>
/// What search engines and chat apps read from the storefront. The storefront's nginx serves all of
/// it from its own domain: the head is spliced into index.html on every page load, for people and
/// crawlers alike, and robots.txt / sitemap.xml are passed straight through.
/// <para>
/// Every answer depends on the host the storefront was loaded from (nginx passes it on). On the
/// platform's host a store's pages sit under "/{slug}"; on a store's own domain they sit at the root.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/seo")]
public class SeoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StorefrontPageMeta _pages;
    private readonly StoreDomains _domains;
    private readonly StorefrontOptions _storefront;

    public SeoController(AppDbContext db, StorefrontPageMeta pages, StoreDomains domains, IOptions<StorefrontOptions> storefront)
    {
        _db = db;
        _pages = pages;
        _domains = domains;
        _storefront = storefront.Value;
    }

    /// <param name="path">The storefront path being loaded, e.g. "{slug}/product/{id}" ("product/{id}" on a store's own domain).</param>
    [HttpGet("head/{**path}")]
    public async Task<IActionResult> Head(string? path, CancellationToken ct)
    {
        var segments = (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        var site = await _domains.FindAsync(Request.Host.Host, ct);

        var page = site is not null
            ? await _pages.FindAsync(site.Slug, segments, ct)
            : segments.Length > 0
                ? await _pages.FindAsync(segments[0], segments[1..], ct)
                : null;

        // An empty answer makes nginx keep index.html's own default head.
        return page is null ? NoContent() : Content(PageHead.Render(page), "text/html; charset=utf-8");
    }

    [HttpGet("robots.txt")]
    public async Task<IActionResult> Robots(CancellationToken ct)
    {
        var site = await _domains.FindAsync(Request.Host.Host, ct);
        var origin = StorefrontLinks.Origin(_storefront.Url, site?.Domain);
        // On the platform every store's pages sit one level down.
        var below = site is null ? "/*" : "";

        // Session-bound pages: nothing on them is worth a search result, and a crawler has no session.
        var robots = $"""
            User-agent: *
            Disallow: {below}/cart
            Disallow: {below}/orders
            Disallow: {below}/welcome

            Sitemap: {origin}/sitemap.xml

            """;

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(robots, "text/plain; charset=utf-8");
    }

    /// <summary>
    /// On the platform: every public store's catalogue. On a store's own domain: that store's.
    /// Invite-only stores are never listed.
    /// </summary>
    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var site = await _domains.FindAsync(Request.Host.Host, ct);
        var query = _db.Stores.AsNoTracking()
            .Where(s => s.IsActive && s.StorefrontAccess == StorefrontAccessMode.Public);

        // A store on its own domain is listed there; the platform's copies of its pages point at it.
        query = site is null
            ? query.Where(s => s.CustomDomain == null)
            : query.Where(s => s.Id == site.Id);

        var stores = await query
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Slug, s.CustomDomain })
            .ToListAsync(ct);
        var storeIds = stores.Select(s => s.Id).ToList();

        // An empty category page is thin content, so only categories with something to buy are listed.
        var categories = (await _db.Categories.AsNoTracking()
                .Where(c => storeIds.Contains(c.StoreId) && c.Products.Any(p => p.IsAvailable))
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new { c.StoreId, c.Id })
                .ToListAsync(ct))
            .ToLookup(c => c.StoreId, c => c.Id);

        var products = (await _db.Products.AsNoTracking()
                .Where(p => storeIds.Contains(p.StoreId) && p.IsAvailable)
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                .Select(p => new { p.StoreId, p.Id })
                .ToListAsync(ct))
            .ToLookup(p => p.StoreId, p => p.Id);

        var urls = new List<string>();
        foreach (var store in stores)
        {
            var storeUrl = StorefrontLinks.StoreBase(_storefront.Url, store.Slug, store.CustomDomain);
            urls.Add(storeUrl);
            urls.AddRange(categories[store.Id].Select(id => $"{storeUrl}/category/{id}"));
            urls.AddRange(products[store.Id].Select(id => $"{storeUrl}/product/{id}"));
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(WriteSitemap(urls), "application/xml; charset=utf-8");
    }

    private static byte[] WriteSitemap(IEnumerable<string> urls)
    {
        using var stream = new MemoryStream();
        using (var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            const string ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", ns);
            foreach (var url in urls)
            {
                xml.WriteStartElement("url", ns);
                xml.WriteElementString("loc", ns, url);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return stream.ToArray();
    }
}
