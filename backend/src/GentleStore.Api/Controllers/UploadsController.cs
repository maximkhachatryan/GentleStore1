using GentleStore.Api.Uploads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    // Generous on purpose: photos straight off a phone are welcome, and are shrunk on arrival.
    private const long MaxBytes = 30 * 1024 * 1024;

    private readonly UploadImages _images;

    public UploadsController(UploadImages images) => _images = images;

    [HttpPost]
    [Authorize]
    // Above Kestrel's ~28 MB default, with room for the multipart wrapping around the file.
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(CancellationToken ct)
    {
        if (!Request.HasFormContentType)
            return BadRequest(new { error = "Expected multipart/form-data." });

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxBytes)
            return BadRequest(new { error = "File exceeds the 30 MB limit." });

        // Only the re-encoded image is kept; the upload itself is just read once from a temp file.
        var temp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.upload");
        try
        {
            await using (var stream = System.IO.File.Create(temp))
                await file.CopyToAsync(stream, ct);

            var fileName = await _images.SaveAsync(temp, ct);
            return fileName is null
                ? BadRequest(new { error = "Unsupported or damaged image." })
                : Ok(new { url = $"/uploads/{fileName}" });
        }
        finally
        {
            System.IO.File.Delete(temp);
        }
    }

    /// <summary>
    /// Sized copies are served as static files. This only runs when one is missing — an upload from
    /// before sizes existed — and makes it on first request.
    /// </summary>
    [HttpGet("~/uploads/{size}/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> Sized(string size, string fileName, CancellationToken ct)
    {
        var path = await _images.EnsureSizeAsync(size, fileName, ct);
        if (path is null) return NotFound();

        Response.Headers.CacheControl = UploadImages.CacheControl;
        return PhysicalFile(path, "image/webp");
    }
}
