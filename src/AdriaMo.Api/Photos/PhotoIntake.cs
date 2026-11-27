using System.Security.Cryptography;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Constats;
using AdriaMo.Core.Photos;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace AdriaMo.Api.Photos;

/// <summary>
/// Takes in a photo (spec §3, §4): keeps the original as received, with its SHA-256; reads the date it was taken; makes the copy
/// that may be published (reduced, upright, re-encoded without any metadata, so without GPS) and its thumbnail.
/// </summary>
internal sealed class PhotoIntake : IDisposable
{
    /// <summary>The largest photo accepted, in bytes.</summary>
    public const long MaxBytes = 25 * 1024 * 1024;

    /// <summary>
    /// The largest photo accepted, in pixels: a small file can claim a huge image. Phones save 12 to 50 megapixels by default, as
    /// JPEG, which is decoded at a reduced scale. A format decoded at full size (PNG) holds 4 bytes a pixel: 160 MB at most, the
    /// reduced copies being small, times <see cref="Parallelism"/>.
    /// </summary>
    public const long MaxPixels = 40_000_000;

    /// <summary>Photos decoded at once: each holds a few hundred megabytes while it is reduced.</summary>
    public const int Parallelism = 2;

    /// <summary>The longest side of the published copy, in pixels.</summary>
    public const int PublishedSize = 1600;

    /// <summary>The longest side of the thumbnail, in pixels.</summary>
    public const int ThumbnailSize = 480;

    /// <summary>The JPEG quality of the published copy.</summary>
    public const int PublishedQuality = 82;

    /// <summary>The JPEG quality of the thumbnail.</summary>
    public const int ThumbnailQuality = 75;

    /// <summary>The folder of the originals, under the data folder.</summary>
    public const string OriginalFolder = "originals";

    /// <summary>The folder of the published copies and thumbnails, under the data folder.</summary>
    public const string DerivedFolder = "photos";

    private readonly string _originals;
    private readonly string _derived;
    private readonly TimeZoneInfo _buildingTimeZone;
    private readonly SemaphoreSlim _decoding = new(Parallelism, Parallelism);

    /// <summary>Creates the intake.</summary>
    /// <param name="storage">The storage settings.</param>
    /// <param name="building">The building settings.</param>
    public PhotoIntake(IOptions<StorageOptions> storage, IOptions<BuildingOptions> building)
    {
        _originals = Path.Join(storage.Value.DataFolder, OriginalFolder);
        _derived = Path.Join(storage.Value.DataFolder, DerivedFolder);
        _buildingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(building.Value.TimeZone);
    }

    /// <summary>The path of a published copy or thumbnail, or <c>null</c> when the name is not one this intake gives.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The path, or <c>null</c>.</returns>
    public string? DerivedPath(string fileName) => IsDerivedName(fileName) ? Path.Join(_derived, fileName) : null;

    /// <summary>Whether a name is one this intake gives to a derived file: 64 lower-case hexadecimal digits and ".jpg".</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsDerivedName(string fileName) =>
        fileName.Length == 68 && fileName.EndsWith(".jpg", StringComparison.Ordinal) && fileName[..64].All(char.IsAsciiHexDigitLower);

    /// <summary>The SHA-256 of a file, in lower-case hexadecimal: the name of an original and the proof it was not changed.</summary>
    /// <param name="content">The file.</param>
    /// <returns>Its hash.</returns>
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Takes in a photo.</summary>
    /// <param name="content">The file as received.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The photo and its date, or the code of the problem: <c>NotAnImage</c>, <c>TooManyPixels</c>.</returns>
    public async Task<IntakeResult> AcceptAsync(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return IntakeResult.Refused("NotAnImage");
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return IntakeResult.Refused("TooManyPixels");
        }

        await _decoding.WaitAsync(cancellationToken);
        try
        {
            using var decoded = Decode(codec);
            if (decoded is null)
            {
                return IntakeResult.Refused("NotAnImage");
            }

            var sha256 = Sha256(content);
            var originalName = sha256 + Extension(codec.EncodedFormat);
            System.IO.Directory.CreateDirectory(_originals);
            System.IO.Directory.CreateDirectory(_derived);
            await WriteOnceAsync(Path.Join(_originals, originalName), content, cancellationToken);

            var (publishedName, width, height) = await WriteReducedAsync(decoded, codec.EncodedOrigin, PublishedSize, PublishedQuality, cancellationToken);
            var (thumbnailName, _, _) = await WriteReducedAsync(decoded, codec.EncodedOrigin, ThumbnailSize, ThumbnailQuality, cancellationToken);
            return new IntakeResult(
                new IntakenPhoto(new PiecePhoto(sha256, originalName, publishedName, thumbnailName, width, height), TakenAt(content)),
                null);
        }
        finally
        {
            _decoding.Release();
        }
    }

    /// <summary>
    /// Deletes the files of a photo that nothing refers to any more (a photo of the inbox that was discarded): the original and its
    /// copies are not kept without a reason.
    /// </summary>
    /// <param name="photo">The files of the photo.</param>
    public void Forget(PiecePhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        File.Delete(Path.Join(_originals, Path.GetFileName(photo.OriginalFileName)));
        foreach (var name in new[] { photo.PublishedFileName, photo.ThumbnailFileName })
        {
            if (DerivedPath(name) is { } path)
            {
                File.Delete(path);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _decoding.Dispose();

    /// <summary>Decodes at the smallest size the codec offers that is still at least twice the published size: a 50-megapixel
    /// photo is never held whole in memory.</summary>
    private static SKBitmap? Decode(SKCodec codec)
    {
        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var scale = Math.Min(1f, 2f * PublishedSize / longest);
        var size = codec.GetScaledDimensions(scale);
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        try
        {
            // A truncated file is refused: its published copy would be half grey.
            if (codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success)
            {
                var decoded = bitmap;
                bitmap = null;
                return decoded;
            }

            return null;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private DateTimeOffset? TakenAt(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var exif = ImageMetadataReader.ReadMetadata(stream).OfType<ExifSubIfdDirectory>().FirstOrDefault();
            return exif is null
                ? null
                : ExifDate.Parse(
                    exif.GetString(ExifDirectoryBase.TagDateTimeOriginal),
                    exif.GetString(ExifDirectoryBase.TagTimeZoneOriginal),
                    _buildingTimeZone);
        }
        catch (Exception exception) when (exception is ImageProcessingException or IOException or ArgumentException or IndexOutOfRangeException)
        {
            // The date is optional: an unreadable EXIF block never refuses the photo, which is then dated by its upload.
            return null;
        }
    }

    /// <summary>Reduces, then turns upright: the turn works on the small copy, never on the full image.</summary>
    private async Task<(string Name, int Width, int Height)> WriteReducedAsync(
        SKBitmap source,
        SKEncodedOrigin origin,
        int longestSide,
        int quality,
        CancellationToken cancellationToken)
    {
        var scale = Math.Min(1d, (double)longestSide / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("The photo could not be resized.");
        using var upright = origin == SKEncodedOrigin.TopLeft ? null : Upright(resized, origin);
        var shown = upright ?? resized;
        using var image = SKImage.FromBitmap(shown);
        // Skia writes no metadata: no EXIF, so no GPS position, no camera serial number.
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        var bytes = encoded.ToArray();
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".jpg";
        await WriteOnceAsync(Path.Join(_derived, name), bytes, cancellationToken);
        return (name, shown.Width, shown.Height);
    }

    /// <summary>Turns the pixels the way the camera said the phone was held.</summary>
    private static SKBitmap Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = swaps ? new SKBitmap(source.Height, source.Width) : new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        float width = result.Width;
        float height = result.Height;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1, width / 2, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, width / 2, height / 2);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1, 0, height / 2);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Scale(-1, 1, width / 2, 0);
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Scale(-1, 1, width / 2, 0);
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
            default:
                break;
        }

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        return result;
    }

    private static string Extension(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => ".jpg",
        SKEncodedImageFormat.Png => ".png",
        SKEncodedImageFormat.Webp => ".webp",
        SKEncodedImageFormat.Heif => ".heic",
        SKEncodedImageFormat.Avif => ".avif",
        SKEncodedImageFormat.Gif => ".gif",
        _ => ".img",
    };

    /// <summary>Writes a file named by its content, once: a second identical file is the same file.</summary>
    private static async Task WriteOnceAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            return;
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            // The same content arrived at the same time: the file is there.
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

/// <summary>What the intake made of a file: a photo, or the code of the problem.</summary>
/// <param name="Photo">The photo, or <c>null</c>.</param>
/// <param name="Problem">The code of the problem, or <c>null</c>.</param>
internal sealed record IntakeResult(IntakenPhoto? Photo, string? Problem)
{
    /// <summary>A refused file.</summary>
    /// <param name="problem">The code of the problem.</param>
    /// <returns>The result.</returns>
    public static IntakeResult Refused(string problem) => new(null, problem);
}

/// <summary>A photo taken in.</summary>
/// <param name="Photo">Its files.</param>
/// <param name="TakenAt">When it was taken, or <c>null</c>.</param>
internal sealed record IntakenPhoto(PiecePhoto Photo, DateTimeOffset? TakenAt);
