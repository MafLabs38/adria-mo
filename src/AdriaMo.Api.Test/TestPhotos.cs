using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace AdriaMo.Api.Test;

/// <summary>Invented photos (a plain colour), with the EXIF fields a phone writes: never a real photo in this repository.</summary>
internal static class TestPhotos
{
    /// <summary>A JPEG, with an EXIF segment when any field is given.</summary>
    public static byte[] Jpeg(
        int width = 64,
        int height = 48,
        string? takenAt = null,
        string? offset = null,
        ushort? orientation = null,
        bool gps = false,
        SKColor? colour = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(colour ?? new SKColor(200, 80, 40));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var jpeg = data.ToArray();
        if (takenAt is null && orientation is null && !gps)
        {
            return jpeg;
        }

        var app1 = Exif(takenAt, offset, orientation, gps);
        return [.. jpeg.AsSpan(0, 2), .. app1, .. jpeg.AsSpan(2)];
    }

    /// <summary>The APP1 segment: "Exif", a little-endian TIFF header, IFD0, the EXIF IFD and the GPS IFD.</summary>
    private static byte[] Exif(string? takenAt, string? offset, ushort? orientation, bool gps)
    {
        var ifd0 = new List<Entry>();
        if (orientation is { } o)
        {
            ifd0.Add(Entry.Short(0x0112, o));
        }

        var exifIfd = new List<Entry>();
        if (takenAt is not null)
        {
            exifIfd.Add(Entry.Ascii(0x9003, takenAt));
        }

        if (offset is not null)
        {
            exifIfd.Add(Entry.Ascii(0x9011, offset));
        }

        var gpsIfd = gps
            ? new List<Entry> { Entry.Ascii(0x0001, "N"), Entry.Rationals(0x0002, (45, 1), (11, 1), (30, 1)) }
            : [];

        // Layout: header (8), IFD0, EXIF IFD, GPS IFD, then the values that do not fit in an entry.
        var ifd0Count = ifd0.Count + (exifIfd.Count > 0 ? 1 : 0) + (gpsIfd.Count > 0 ? 1 : 0);
        var ifd0Size = IfdSize(ifd0Count);
        var exifOffset = 8 + ifd0Size;
        var gpsOffset = exifOffset + (exifIfd.Count > 0 ? IfdSize(exifIfd.Count) : 0);
        var valuesOffset = gpsOffset + (gpsIfd.Count > 0 ? IfdSize(gpsIfd.Count) : 0);
        if (exifIfd.Count > 0)
        {
            ifd0.Add(Entry.Long(0x8769, (uint)exifOffset));
        }

        if (gpsIfd.Count > 0)
        {
            ifd0.Add(Entry.Long(0x8825, (uint)gpsOffset));
        }

        var tiff = new List<byte>();
        tiff.AddRange("II"u8.ToArray());
        tiff.AddRange(U16(42));
        tiff.AddRange(U32(8));
        var values = new List<byte>();
        foreach (var ifd in new[] { ifd0, exifIfd, gpsIfd }.Where(i => i.Count > 0))
        {
            tiff.AddRange(U16((ushort)ifd.Count));
            foreach (var entry in ifd.OrderBy(e => e.Tag))
            {
                tiff.AddRange(U16(entry.Tag));
                tiff.AddRange(U16(entry.Type));
                tiff.AddRange(U32(entry.Count));
                if (entry.Value.Length <= 4)
                {
                    tiff.AddRange([.. entry.Value, .. new byte[4 - entry.Value.Length]]);
                }
                else
                {
                    tiff.AddRange(U32((uint)(valuesOffset + values.Count)));
                    values.AddRange(entry.Value);
                }
            }

            tiff.AddRange(U32(0));
        }

        tiff.AddRange(values);
        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));
        return [0xFF, 0xE1, .. length, .. payload];
    }

    private static int IfdSize(int entries) => 2 + (12 * entries) + 4;

    private static byte[] U16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private sealed record Entry(ushort Tag, ushort Type, uint Count, byte[] Value)
    {
        public static Entry Short(ushort tag, ushort value) => new(tag, 3, 1, U16(value));

        public static Entry Long(ushort tag, uint value) => new(tag, 4, 1, U32(value));

        public static Entry Ascii(ushort tag, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value + "\0");
            return new(tag, 2, (uint)bytes.Length, bytes);
        }

        public static Entry Rationals(ushort tag, params (uint Numerator, uint Denominator)[] values) =>
            new(tag, 5, (uint)values.Length, [.. values.SelectMany(v => U32(v.Numerator).Concat(U32(v.Denominator)))]);
    }
}
