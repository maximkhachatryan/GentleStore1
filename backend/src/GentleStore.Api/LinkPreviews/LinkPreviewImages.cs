using GentleStore.Api.Uploads;
using SkiaSharp;

namespace GentleStore.Api.LinkPreviews;

public sealed record PreviewImage(string UploadFileName, int Width, int Height);

/// <summary>
/// Small JPEG copies of uploaded photos for link previews. WhatsApp skips preview images much over
/// ~300 KB, which even the capped uploads exceed, and JPEG is the format chat apps' crawlers most
/// reliably take, so the stored WebP sizes are not used here.
/// Uploads are immutable (random names), so each copy is made once and kept next to them.
/// </summary>
public sealed class LinkPreviewImages
{
    private const int MaxSide = 800;
    private const int JpegQuality = 80;

    private readonly string _uploads;
    private readonly string _previews;
    private readonly ILogger<LinkPreviewImages> _log;

    public LinkPreviewImages(IWebHostEnvironment env, ILogger<LinkPreviewImages> log)
    {
        _uploads = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        _previews = Path.Combine(_uploads, "previews");
        _log = log;
    }

    public string PreviewPath(string uploadFileName) =>
        Path.Combine(_previews, Path.GetFileNameWithoutExtension(uploadFileName) + ".jpg");

    /// <summary>Makes the preview copy if it does not exist yet. Null when the upload is missing or unreadable.</summary>
    public async Task<PreviewImage?> EnsureAsync(string uploadFileName, CancellationToken ct = default)
    {
        // The name doubles as a path segment, so nothing but our own upload names gets near the disk.
        if (!UploadImages.IsUploadName(uploadFileName)) return null;

        var target = PreviewPath(uploadFileName);
        if (File.Exists(target))
        {
            using var cached = SKCodec.Create(File.OpenRead(target));
            return cached is null ? null : new PreviewImage(uploadFileName, cached.Info.Width, cached.Info.Height);
        }

        var source = Path.Combine(_uploads, uploadFileName);
        if (!File.Exists(source)) return null;

        try
        {
            // JPEG has no transparency; a transparent PNG would otherwise come out black.
            using var bitmap = ImageResizer.Load(source, MaxSide, SKColors.White);
            if (bitmap is null) return null;
            var bytes = ImageResizer.Encode(bitmap, SKEncodedImageFormat.Jpeg, JpegQuality);

            Directory.CreateDirectory(_previews);
            // Written aside and moved into place, so a concurrent request never serves half a file.
            var temp = $"{target}.{Guid.NewGuid():n}.tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, target, overwrite: true);

            return new PreviewImage(uploadFileName, bitmap.Width, bitmap.Height);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not make a link preview image for {File}.", uploadFileName);
            return null;
        }
    }
}
