using SkiaSharp;

namespace GentleStore.Api.Uploads;

/// <summary>Decoding and downscaling shared by the stored image sizes and the link-preview copies.</summary>
public static class ImageResizer
{
    // Decoded pixels cost 4 bytes each. JPEG and WebP decode straight to a fraction of their size
    // (see DecodeSize), so in practice only a giant PNG or GIF ever gets near this.
    private const long MaxDecodedPixels = 50_000_000;

    /// <summary>
    /// The image at <paramref name="path"/>, upright and no larger than <paramref name="maxSide"/> on
    /// its long side. Never enlarges. Null when the file is not an image Skia can read, or is too big
    /// to decode safely. A <paramref name="background"/> flattens transparency, for formats without it.
    /// </summary>
    public static SKBitmap? Load(string path, int maxSide, SKColor? background = null)
    {
        using var codec = SKCodec.Create(path);
        if (codec is null) return null;

        var size = DecodeSize(codec, maxSide);
        if ((long)size.Width * size.Height > MaxDecodedPixels) return null;

        var info = new SKImageInfo(
            size.Width, size.Height, SKImageInfo.PlatformColorType,
            codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul,
            // Phones shoot in wider gamuts (iPhones in Display P3). Decoding into sRGB converts the
            // colours; left as they are, browsers would show them washed out.
            SKColorSpace.CreateSrgb());

        using var decoded = SKBitmap.Decode(codec, info);
        return decoded is null ? null : Resize(decoded, maxSide, codec.EncodedOrigin, background);
    }

    /// <summary>
    /// A new bitmap no larger than <paramref name="maxSide"/> on its long side, turned upright for
    /// <paramref name="origin"/>. Never enlarges.
    /// </summary>
    public static SKBitmap Resize(
        SKBitmap source, int maxSide, SKEncodedOrigin origin = SKEncodedOrigin.TopLeft, SKColor? background = null)
    {
        // Phone photos are stored sideways with an EXIF hint; browsers honour it, a raw decode does not.
        var quarterTurn = origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom;

        var scale = Math.Min(1f, (float)maxSide / Math.Max(source.Width, source.Height));
        var drawW = Math.Max(1, (int)Math.Round(source.Width * scale));
        var drawH = Math.Max(1, (int)Math.Round(source.Height * scale));
        var (outW, outH) = quarterTurn ? (drawH, drawW) : (drawW, drawH);

        var result = new SKBitmap(new SKImageInfo(
            outW, outH, source.ColorType, background is null ? source.AlphaType : SKAlphaType.Opaque, source.ColorSpace));
        using var canvas = new SKCanvas(result);
        canvas.Clear(background ?? SKColors.Transparent);

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

        // Cubic is sharpest for modest reductions but aliases fine detail beyond about 2×;
        // mipmaps average properly at any ratio.
        var sampling = scale < 0.5f
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, new SKRect(0, 0, drawW, drawH), sampling, null);
        return result;
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var pixmap = bitmap.PeekPixels();
        // The pixels are already sRGB (see Load). Untagged, browsers read them as sRGB anyway, and the
        // file skips an embedded colour profile that would be a sizeable share of a small thumbnail.
        using var untagged = pixmap.WithColorSpace(null!);
        using var data = untagged.Encode(format, quality)
                         ?? throw new InvalidOperationException($"Skia could not encode a {format} image.");
        return data.ToArray();
    }

    // JPEG decodes straight to 1/8 … 8/8 of its size and WebP to any size; other formats only at
    // full size. Take the smallest that is still at least maxSide, so a 50-megapixel photo is
    // never held in memory at full size just to be shrunk.
    private static SKSizeI DecodeSize(SKCodec codec, int maxSide)
    {
        for (var eighths = 1; eighths < 8; eighths++)
        {
            var size = codec.GetScaledDimensions(eighths / 8f);
            if (Math.Max(size.Width, size.Height) >= maxSide) return size;
        }

        return codec.Info.Size;
    }
}
