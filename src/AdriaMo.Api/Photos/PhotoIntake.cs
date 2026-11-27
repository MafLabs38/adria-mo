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
internal sealed class PhotoIntake
{
    /// <summary>The largest photo accepted, in bytes.</summary>
    public const long MaxBytes = 25 * 1024 * 1024;

    /// <summary>The longest side of the published copy, in pixels.</summary>
    public const int PublishedSize = 1600;

    /// <summary>The longest side of the thumbnail, in pixels.</summary>
    public const int ThumbnailSize = 480;

    /// <summary>The folder of the originals, under the data folder.</summary>
    public const string OriginalFolder = "originals";

    /// <summary>The folder of the published copies and thumbnails, under the data folder.</summary>
    public const string DerivedFolder = "photos";

    private readonly string _originals;
    private readonly string _derived;
    private readonly TimeZoneInfo _buildingTimeZone;

    /// <summary>Creates the intake.</summary>
    /// <param name="storage">The storage settings.</param>
    /// <param name="building">The building settings.</param>
    public PhotoIntake(IOptions<StorageOptions> storage, IOptions<BuildingOptions> building)
    {
        _originals = Path.Combine(storage.Value.DataFolder, OriginalFolder);
        _derived = Path.Combine(storage.Value.DataFolder, DerivedFolder);
        _buildingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(building.Value.TimeZone);
    }

    /// <summary>The path of a published copy or thumbnail, or <c>null</c> when the name is not one this intake gives.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The path, or <c>null</c>.</returns>
    public string? DerivedPath(string fileName) => IsDerivedName(fileName) ? Path.Combine(_derived, fileName) : null;

    /// <summary>Whether a name is one this intake gives to a derived file: 64 lower-case hexadecimal digits and ".jpg".</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsDerivedName(string fileName) =>
        fileName.Length == 68 && fileName.EndsWith(".jpg", StringComparison.Ordinal) && fileName[..64].All(char.IsAsciiHexDigitLower);

    /// <summary>Takes in a photo.</summary>
    /// <param name="content">The file as received.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The photo and its date, or <c>null</c> when the file is not an image this intake can read.</returns>
    public async Task<IntakenPhoto?> AcceptAsync(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var codec = SKCodec.Create(SKData.CreateCopy(content));
        if (codec is null)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var originalName = sha256 + Extension(codec.EncodedFormat);
        System.IO.Directory.CreateDirectory(_originals);
        System.IO.Directory.CreateDirectory(_derived);
        await WriteOnceAsync(Path.Combine(_originals, originalName), content, cancellationToken);

        using var upright = Upright(decoded, codec.EncodedOrigin);
        var (publishedName, width, height) = await WriteReducedAsync(upright, PublishedSize, 82, cancellationToken);
        var (thumbnailName, _, _) = await WriteReducedAsync(upright, ThumbnailSize, 75, cancellationToken);

        return new IntakenPhoto(
            new PiecePhoto(sha256, originalName, publishedName, thumbnailName, width, height),
            TakenAt(content));
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
        catch (ImageProcessingException)
        {
            return null;
        }
    }

    private async Task<(string Name, int Width, int Height)> WriteReducedAsync(SKBitmap source, int longestSide, int quality, CancellationToken cancellationToken)
    {
        var scale = Math.Min(1d, (double)longestSide / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("The photo could not be resized.");
        using var image = SKImage.FromBitmap(resized);
        // Skia writes no metadata: no EXIF, so no GPS position, no camera serial number.
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        var bytes = encoded.ToArray();
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".jpg";
        await WriteOnceAsync(Path.Combine(_derived, name), bytes, cancellationToken);
        return (name, width, height);
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
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            File.Delete(temporary);
        }
    }
}

/// <summary>A photo taken in.</summary>
/// <param name="Photo">Its files.</param>
/// <param name="TakenAt">When it was taken, or <c>null</c>.</param>
internal sealed record IntakenPhoto(PiecePhoto Photo, DateTimeOffset? TakenAt);
