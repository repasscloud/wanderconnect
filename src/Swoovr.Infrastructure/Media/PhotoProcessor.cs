using SkiaSharp;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Media;

/// <summary>
/// Validates an upload and makes the copy Swoovr keeps (policy §5, §10; D203): the type is recognised from the bytes
/// (JPEG, PNG or WebP), animated images are refused, size and pixel limits are checked before decoding, the EXIF
/// orientation is applied, and the image is re-encoded as JPEG, which carries no EXIF, GPS or other metadata. The
/// original bytes are never stored.
/// </summary>
public sealed class PhotoProcessor : IPhotoProcessor
{
    public ProcessedPhoto Process(byte[] upload)
    {
        if (upload.Length == 0) return Fail(MediaUploadError.Empty);
        if (upload.Length > MediaPolicy.MaxUploadBytes) return Fail(MediaUploadError.TooBig);
        var type = MediaPolicy.Sniff(upload);
        if (type is null) return Fail(MediaUploadError.UnsupportedType);
        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data);
        if (codec is null) return Fail(MediaUploadError.Undecodable, type);
        if (codec.FrameCount > 1) return Fail(MediaUploadError.Animated, type);
        var size = MediaPolicy.CheckSize(codec.Info.Width, codec.Info.Height);
        if (size != MediaUploadError.None) return Fail(size, type);
        using var decoded = SKBitmap.Decode(codec, codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul));
        if (decoded is null) return Fail(MediaUploadError.Undecodable, type);
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var scale = Math.Min(1.0, (double)MediaPolicy.StoredMaxSide / Math.Max(oriented.Width, oriented.Height));
        var (width, height) = ((int)Math.Round(oriented.Width * scale), (int)Math.Round(oriented.Height * scale));
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        // Transparent areas become white rather than black in the JPEG.
        surface.Canvas.Clear(SKColors.White);
        using var paint = new SKPaint();
        using var source = SKImage.FromBitmap(oriented);
        surface.Canvas.DrawImage(source, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, MediaPolicy.JpegQuality);
        if (encoded is null) return Fail(MediaUploadError.Undecodable, type);
        return new(MediaUploadError.None, encoded.ToArray(), width, height, type);
    }

    private static ProcessedPhoto Fail(MediaUploadError error, string? type = null) => new(error, [], 0, 0, type);

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bitmap.Copy();
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
        using var canvas = new SKCanvas(result);
        var (w, h) = (bitmap.Width, bitmap.Height);
        // Canvas calls apply to points in reverse order; (x, y) is a source pixel.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;               // (w-x, y)
            case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;      // (w-x, h-y)
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;             // (x, h-y)
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;              // (y, x)
            case SKEncodedOrigin.RightTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); break;          // (h-y, x)
            case SKEncodedOrigin.RightBottom: canvas.Translate(h, w); canvas.Scale(-1, -1); canvas.RotateDegrees(90); canvas.Scale(1, -1); break; // (h-y, w-x)
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, w); canvas.RotateDegrees(-90); break;       // (y, w-x)
        }
        canvas.DrawBitmap(bitmap, 0, 0);
        return result;
    }
}
