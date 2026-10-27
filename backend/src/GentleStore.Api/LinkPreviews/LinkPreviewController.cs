using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.LinkPreviews;

/// <summary>
/// The pictures chat apps show when a storefront link is shared. The page's Open Graph tags (see
/// Seo/PageHead) point here through the storefront's own domain, at /_preview/image/{file}.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/link-preview")]
public class LinkPreviewController : ControllerBase
{
    private readonly LinkPreviewImages _images;

    public LinkPreviewController(LinkPreviewImages images) => _images = images;

    [HttpGet("image/{fileName}")]
    public async Task<IActionResult> Image(string fileName, CancellationToken ct)
    {
        var image = await _images.EnsureAsync(fileName, ct);
        if (image is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(_images.PreviewPath(fileName), "image/jpeg");
    }
}
