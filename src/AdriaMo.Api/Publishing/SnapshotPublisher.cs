using System.Text.Json;
using System.Threading.Channels;
using AdriaMo.Api.Data;
using AdriaMo.Api.Photos;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Publishing;

/// <summary>Asks for a publication; the requests made during the gathering delay give one publication.</summary>
internal sealed class PublishQueue
{
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    });

    /// <summary>The waiting requests, read by the publisher.</summary>
    public ChannelReader<bool> Reader => _requests.Reader;

    /// <summary>Asks for a publication after a change of the public data.</summary>
    public void Request() => _requests.Writer.TryWrite(true);
}

/// <summary>
/// Publishes the public snapshot (spec §10.2): <c>constats.json</c> and the photos of public constats, into the publish folder
/// the static server or the CDN serves. Publishes at start, then after each change (gathered for <see cref="PublishOptions.Delay"/>).
/// A photo that is no longer public leaves the folder.
/// </summary>
/// <param name="services">The root services.</param>
/// <param name="queue">The requests.</param>
/// <param name="options">The publication settings.</param>
/// <param name="storage">The storage settings.</param>
/// <param name="building">The building settings.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed partial class SnapshotPublisher(
    IServiceProvider services,
    PublishQueue queue,
    IOptions<PublishOptions> options,
    IOptions<StorageOptions> storage,
    IOptions<BuildingOptions> building,
    TimeProvider time,
    ILogger<SnapshotPublisher> logger) : BackgroundService
{
    /// <summary>The name of the published data file.</summary>
    public const string SnapshotFile = "constats.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryPublishAsync(stoppingToken);
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            await Task.Delay(options.Value.Delay, time, stoppingToken);
            while (queue.Reader.TryRead(out _))
            {
                // Every request made until now is served by the publication below.
            }

            await TryPublishAsync(stoppingToken);
        }
    }

    /// <summary>Publishes now.</summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The snapshot published.</returns>
    public async Task<PublicSnapshot> PublishAsync(CancellationToken cancellationToken)
    {
        await _oneAtATime.WaitAsync(cancellationToken);
        try
        {
            return await PublishOnceAsync(cancellationToken);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _oneAtATime.Dispose();
        base.Dispose();
    }

    private async Task<PublicSnapshot> PublishOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdriaMoDbContext>();
        var constats = await db.Constats.AsNoTracking().Include(c => c.Pieces).ToListAsync(cancellationToken);

        var target = storage.Value.PublishFolder;
        var photos = Path.Combine(target, PublicSnapshotBuilder.PhotoFolder);
        Directory.CreateDirectory(photos);

        var files = PublicSnapshotBuilder.PublishedFiles(constats);
        foreach (var file in files)
        {
            var destination = Path.Combine(photos, file);
            if (!File.Exists(destination))
            {
                File.Copy(Path.Combine(storage.Value.DataFolder, PhotoIntake.DerivedFolder, file), destination);
            }
        }

        var snapshot = PublicSnapshotBuilder.Build(constats, time.GetUtcNow(), building.Value.TimeZone);
        var temporary = Path.Combine(target, SnapshotFile + ".tmp");
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, Json, cancellationToken);
        }

        File.Move(temporary, Path.Combine(target, SnapshotFile), overwrite: true);

        // After the new snapshot: a page never refers to a photo already removed.
        foreach (var stale in Directory.EnumerateFiles(photos).Where(f => !files.Contains(Path.GetFileName(f))))
        {
            File.Delete(stale);
        }

        LogPublished(logger, snapshot.Constats.Count, files.Count);
        return snapshot;
    }

    private async Task TryPublishAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PublishAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DbUpdateException or InvalidOperationException)
        {
            // The public page keeps the previous snapshot; the next change or start publishes again.
            LogFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Constats} constats and {Files} photo files.")]
    private static partial void LogPublished(ILogger logger, int constats, int files);

    [LoggerMessage(Level = LogLevel.Error, Message = "The publication failed; the previous snapshot stays online.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
