using System.Text.RegularExpressions;
using SkiaSharp;

namespace GentleStore.Api.Uploads;

public sealed record ImageSize(string Name, int MaxSide, int Quality);

/// <summary>
/// Photos arrive straight off phones — several MB and thousands of pixels across — but are mostly
/// shown far smaller, often over a slow connection. Each upload is kept as a capped master plus WebP
/// copies in a few sizes at /uploads/{size}/{id}.webp, and the apps pick the size that suits where
/// the picture is shown (resolveImageUrl in the shared frontend package). Upload names are random
/// and never reused, so each copy is made once and never goes stale.
/// </summary>
public sealed partial class UploadImages
{
    /// <summary>
    /// Largest first: each size is made from the one before it. The frontend names these too, so
    /// keep the two in step; changing a size's pixels means clearing its directory, as browsers are
    /// told the files never change.
    /// </summary>
    public static readonly ImageSize[] Sizes =
    [
        new("lg", 1600, 85), // the product page
        new("md", 640, 80),  // storefront product cards
        new("sm", 320, 75),  // thumbnails: cart lines, the gallery strip, console lists, logos
    ];

    public const string CacheControl = "public, max-age=31536000, immutable";

    // What is kept of the upload itself. Re-encoding also drops the EXIF block, which on a phone
    // photo includes where it was taken; the master is publicly reachable at its URL.
    private const int MasterMaxSide = 2560;
    private const int MasterQuality = 90;

    private const string UploadsPrefix = "/uploads/";
    private static readonly string[] SourceExtensions = [".webp", ".jpg", ".jpeg", ".png", ".gif"];

    // A decode can take tens of MB. A batch upload, or a list full of photos from before sizes
    // existed, should not run dozens at once on a small server.
    private readonly SemaphoreSlim _gate = new(2);
    private readonly string _uploads;
    private readonly ILogger<UploadImages> _log;

    public UploadImages(IWebHostEnvironment env, ILogger<UploadImages> log)
    {
        _uploads = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        _log = log;
    }

    /// <summary>The upload's file name for a stored "/uploads/…" URL; null for anything else.</summary>
    public static string? UploadFileName(string? imageUrl)
    {
        if (imageUrl is null || !imageUrl.StartsWith(UploadsPrefix, StringComparison.Ordinal)) return null;
        var name = imageUrl[UploadsPrefix.Length..];
        return IsUploadName(name) ? name : null;
    }

    /// <summary>Whether this is one of our own upload names, and so safe to use as a path segment.</summary>
    public static bool IsUploadName(string fileName) => UploadName().IsMatch(fileName);

    /// <summary>
    /// Keeps the image at <paramref name="sourcePath"/> as a new upload and makes its sizes. Returns
    /// the stored file name, or null when the file is not an image that can be read.
    /// </summary>
    public async Task<string?> SaveAsync(string sourcePath, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var master = ImageResizer.Load(sourcePath, MasterMaxSide);
            if (master is null) return null;

            var id = Guid.NewGuid().ToString("n");
            await WriteAsync(Path.Combine(_uploads, id + ".webp"),
                ImageResizer.Encode(master, SKEncodedImageFormat.Webp, MasterQuality), ct);
            await WriteSizesAsync(id, master, ct);
            return id + ".webp";
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The path of a sized copy, made now if missing — as it is for uploads from before sizes
    /// existed. Null for an unknown size or upload, or one that cannot be read.
    /// </summary>
    public async Task<string?> EnsureSizeAsync(string sizeName, string fileName, CancellationToken ct)
    {
        // Both end up in the path, so nothing but known sizes and our own names gets near the disk.
        var size = Sizes.FirstOrDefault(s => s.Name == sizeName);
        if (size is null || SizedName().Match(fileName) is not { Success: true } match) return null;

        var id = match.Groups[1].Value;
        var target = SizedPath(size, id);
        if (File.Exists(target)) return target;

        var source = SourceExtensions.Select(ext => Path.Combine(_uploads, id + ext)).FirstOrDefault(File.Exists);
        if (source is null) return null;

        await _gate.WaitAsync(ct);
        try
        {
            // Another request for the same upload may have made it while this one waited.
            if (File.Exists(target)) return target;

            using var largest = ImageResizer.Load(source, Sizes[0].MaxSide);
            if (largest is null) return null;

            await WriteSizesAsync(id, largest, ct);
            return target;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not make sized copies of upload {Id}.", id);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string SizedPath(ImageSize size, string id) => Path.Combine(_uploads, size.Name, id + ".webp");

    private async Task WriteSizesAsync(string id, SKBitmap source, CancellationToken ct)
    {
        // Each size from the one before: every step stays a modest reduction, which keeps fine
        // detail sharp, and is cheaper than going back to the full image.
        var current = source;
        try
        {
            foreach (var size in Sizes)
            {
                var next = ImageResizer.Resize(current, size.MaxSide);
                if (current != source) current.Dispose();
                current = next;

                await WriteAsync(SizedPath(size, id),
                    ImageResizer.Encode(current, SKEncodedImageFormat.Webp, size.Quality), ct);
            }
        }
        finally
        {
            if (current != source) current.Dispose();
        }
    }

    private static async Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Written aside and moved into place, so a concurrent request never serves half a file.
        var temp = $"{path}.{Guid.NewGuid():n}.tmp";
        await File.WriteAllBytesAsync(temp, bytes, ct);
        File.Move(temp, path, overwrite: true);
    }

    [GeneratedRegex(@"^[a-f0-9]{32}\.(jpg|jpeg|png|webp|gif)$")]
    private static partial Regex UploadName();

    [GeneratedRegex(@"^([a-f0-9]{32})\.webp$")]
    private static partial Regex SizedName();
}
