using System.Text.RegularExpressions;
using SkiaSharp;

namespace GentleStore.Api.LinkPreviews;

public sealed record PreviewImage(string UploadFileName, int Width, int Height);

/// <summary>
/// Small JPEG copies of uploaded photos for link previews. WhatsApp skips preview images much over
/// ~300 KB, and phone photos are uploaded at several MB, so the original cannot be used directly.
/// Uploads are immutable (random names), so each copy is made once and kept next to them.
/// </summary>
public sealed partial class LinkPreviewImages
{
    private const int MaxSide = 800;
    private const int JpegQuality = 80;
    private const string UploadsPrefix = "/uploads/";

    private readonly string _uploads;
    private readonly string _previews;
    private readonly ILogger<LinkPreviewImages> _log;

    public LinkPreviewImages(IWebHostEnvironment env, ILogger<LinkPreviewImages> log)
    {
        _uploads = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        _previews = Path.Combine(_uploads, "previews");
        _log = log;
    }

    /// <summary>The upload's file name for a stored "/uploads/…" URL; null for anything else.</summary>
    public static string? UploadFileName(string? imageUrl)
    {
        if (imageUrl is null || !imageUrl.StartsWith(UploadsPrefix, StringComparison.Ordinal)) return null;
        var name = imageUrl[UploadsPrefix.Length..];
        return UploadName().IsMatch(name) ? name : null;
    }

    public string PreviewPath(string uploadFileName) =>
        Path.Combine(_previews, Path.GetFileNameWithoutExtension(uploadFileName) + ".jpg");

    /// <summary>Makes the preview copy if it does not exist yet. Null when the upload is missing or unreadable.</summary>
    public async Task<PreviewImage?> EnsureAsync(string uploadFileName, CancellationToken ct = default)
    {
        // The name doubles as a path segment, so nothing but our own upload names gets near the disk.
        if (!UploadName().IsMatch(uploadFileName)) return null;

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
            var (bytes, width, height) = Render(source);
            if (bytes is null) return null;

            Directory.CreateDirectory(_previews);
            // Written aside and moved into place, so a concurrent request never serves half a file.
            var temp = $"{target}.{Guid.NewGuid():n}.tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, target, overwrite: true);

            return new PreviewImage(uploadFileName, width, height);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not make a link preview image for {File}.", uploadFileName);
            return null;
        }
    }

    private static (byte[]? Bytes, int Width, int Height) Render(string source)
    {
        using var codec = SKCodec.Create(File.OpenRead(source));
        if (codec is null) return (null, 0, 0);

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return (null, 0, 0);

        // Phone photos are stored sideways with an EXIF hint; browsers honour it, a raw decode does not.
        var origin = codec.EncodedOrigin;
        var quarterTurn = origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom;

        var scale = Math.Min(1f, (float)MaxSide / Math.Max(bitmap.Width, bitmap.Height));
        var drawW = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var drawH = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        var (outW, outH) = quarterTurn ? (drawH, drawW) : (drawW, drawH);

        using var surface = SKSurface.Create(new SKImageInfo(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        // JPEG has no transparency; a transparent PNG would otherwise come out black.
        canvas.Clear(SKColors.White);

        switch (origin)
        {
            case SKEncodedOrigin.RightTop:
                canvas.Translate(outW, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(outW, outH);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, outH);
                canvas.RotateDegrees(270);
                break;
        }

        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, new SKRect(0, 0, drawW, drawH), new SKSamplingOptions(SKCubicResampler.Mitchell), null);

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return (data?.ToArray(), outW, outH);
    }

    [GeneratedRegex(@"^[a-f0-9]{32}\.(jpg|jpeg|png|webp|gif)$")]
    private static partial Regex UploadName();
}
