namespace AdriaMo.Api.Photos;

/// <summary>A photo sent in a form: the checks done before it is read, and its reading.</summary>
internal static class PhotoUpload
{
    /// <summary>The largest request of an endpoint that takes a photo: the photo and a short form around it.</summary>
    public const long RequestLimit = PhotoIntake.MaxBytes + (1024 * 1024);

    /// <summary>The problem of a file before it is read: <c>EmptyFile</c>, <c>TooLarge</c>, or <c>null</c>.</summary>
    /// <param name="photo">The file.</param>
    /// <returns>The code of the problem, or <c>null</c>.</returns>
    public static string? SizeProblem(IFormFile photo) => photo.Length switch
    {
        0 => "EmptyFile",
        > PhotoIntake.MaxBytes => "TooLarge",
        _ => null,
    };

    /// <summary>Reads the file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>Its bytes.</returns>
    public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
