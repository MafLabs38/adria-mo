using AdriaMo.Api.Settings;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Publishing;

/// <summary>
/// Where the public snapshot goes (spec §10.2): a folder served by a static server today, Cloudflare R2 in production. Every write
/// is atomic: a reader sees the old file or the new one, never a part of it.
/// </summary>
internal interface IPublishTarget
{
    /// <summary>The names of the photos already published.</summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The names.</returns>
    Task<IReadOnlySet<string>> ListPhotosAsync(CancellationToken cancellationToken);

    /// <summary>Publishes a photo under its name (its content never changes).</summary>
    /// <param name="name">The name.</param>
    /// <param name="source">The local file to publish.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The task.</returns>
    Task PutPhotoAsync(string name, string source, CancellationToken cancellationToken);

    /// <summary>Replaces the snapshot.</summary>
    /// <param name="content">The new <c>constats.json</c>.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The task.</returns>
    Task PutSnapshotAsync(byte[] content, CancellationToken cancellationToken);

    /// <summary>Removes a photo that is no longer public.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The task.</returns>
    Task DeletePhotoAsync(string name, CancellationToken cancellationToken);
}

/// <summary>
/// The publish folder (<see cref="StorageOptions.PublishFolder"/>): <c>constats.json</c> and <c>photos/</c>. Files are written
/// under a hidden temporary name in the data folder, on the same volume, then moved into place.
/// </summary>
/// <param name="storage">The storage settings.</param>
internal sealed class FolderPublishTarget(IOptions<StorageOptions> storage) : IPublishTarget
{
    private string Photos => Path.Join(storage.Value.PublishFolder, Core.Publishing.PublicSnapshotBuilder.PhotoFolder);

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> ListPhotosAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Photos);
        IReadOnlySet<string> names = Directory.EnumerateFiles(Photos).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(names);
    }

    /// <inheritdoc />
    public async Task PutPhotoAsync(string name, string source, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(source);
        await PutAsync(Path.Join(Photos, name), input, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PutSnapshotAsync(byte[] content, CancellationToken cancellationToken)
    {
        await using var input = new MemoryStream(content, writable: false);
        await PutAsync(Path.Join(storage.Value.PublishFolder, SnapshotPublisher.SnapshotFile), input, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeletePhotoAsync(string name, CancellationToken cancellationToken)
    {
        File.Delete(Path.Join(Photos, name));
        return Task.CompletedTask;
    }

    private async Task PutAsync(string destination, Stream content, CancellationToken cancellationToken)
    {
        // Never in the served folder: a half-written file must not be reachable, even under another name.
        var staging = Path.Join(storage.Value.DataFolder, "publishing");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = Path.Join(staging, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = File.Create(temporary))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
