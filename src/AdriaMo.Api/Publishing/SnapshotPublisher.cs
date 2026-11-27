using System.Text.Json;
using System.Threading.Channels;
using AdriaMo.Api.Data;
using AdriaMo.Api.Photos;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Constats;
using AdriaMo.Core.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Publishing;

/// <summary>The settings the publisher reads.</summary>
/// <param name="publish">The publication settings.</param>
/// <param name="storage">The storage settings.</param>
/// <param name="building">The building settings.</param>
internal sealed class PublisherSettings(IOptions<PublishOptions> publish, IOptions<StorageOptions> storage, IOptions<BuildingOptions> building)
{
    /// <summary>How long changes are gathered.</summary>
    public TimeSpan Delay => publish.Value.Delay;

    /// <summary>The private data folder, which holds the derived photos.</summary>
    public string DataFolder => storage.Value.DataFolder;

    /// <summary>The building's time zone.</summary>
    public string TimeZone => building.Value.TimeZone;
}

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
/// the static server or the CDN serves. Publishes at start, after each change (gathered for <see cref="PublishOptions.Delay"/>),
/// and once a day at least. A failure is recorded in <see cref="PublicationStatus"/> and retried, waiting longer each time. A
/// photo that is no longer public leaves the folder.
/// </summary>
/// <param name="services">The root services.</param>
/// <param name="queue">The requests.</param>
/// <param name="status">The outcome of the publications.</param>
/// <param name="target">Where the snapshot goes.</param>
/// <param name="settings">The settings it needs.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed partial class SnapshotPublisher(
    IServiceProvider services,
    PublishQueue queue,
    PublicationStatus status,
    IPublishTarget target,
    PublisherSettings settings,
    TimeProvider time,
    ILogger<SnapshotPublisher> logger) : BackgroundService
{
    /// <summary>The name of the published data file.</summary>
    public const string SnapshotFile = "constats.json";

    /// <summary>The longest time between two publications.</summary>
    public static readonly TimeSpan Daily = TimeSpan.FromDays(1);

    /// <summary>The first wait before a failed publication is tried again; it doubles up to <see cref="MaxRetryDelay"/>.</summary>
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>The longest wait before a failed publication is tried again.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = FirstRetryDelay;
        var succeeded = await TryPublishAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await WaitForRequestAsync(succeeded ? Daily : retry, stoppingToken))
            {
                await Task.Delay(settings.Delay, time, stoppingToken);
            }

            while (queue.Reader.TryRead(out _))
            {
                // Every request made until now is served by the publication below.
            }

            succeeded = await TryPublishAsync(stoppingToken);
            retry = succeeded ? FirstRetryDelay : TimeSpan.FromTicks(Math.Min(retry.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    /// <summary>Waits for a request, or for the given time.</summary>
    /// <returns>Whether a request came.</returns>
    private async Task<bool> WaitForRequestAsync(TimeSpan timeout, CancellationToken stoppingToken)
    {
        using var timer = new CancellationTokenSource(timeout, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timer.Token);
        try
        {
            return await queue.Reader.WaitToReadAsync(either.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return false;
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

        var derived = Path.Join(settings.DataFolder, PhotoIntake.DerivedFolder);
        bool IsAvailable(PiecePhoto photo) =>
            File.Exists(Path.Join(derived, photo.PublishedFileName)) && File.Exists(Path.Join(derived, photo.ThumbnailFileName));
        var files = PublicSnapshotBuilder.PublishedFiles(constats, IsAvailable);
        var snapshot = PublicSnapshotBuilder.Build(constats, time.GetUtcNow(), settings.TimeZone, IsAvailable);

        // The order keeps every published snapshot whole: its photos first, then the snapshot, then the photos it no longer shows.
        var published = await target.ListPhotosAsync(cancellationToken);
        foreach (var file in files.Where(f => !published.Contains(f)))
        {
            await target.PutPhotoAsync(file, Path.Join(derived, file), cancellationToken);
        }

        await target.PutSnapshotAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot, Json), cancellationToken);

        foreach (var stale in published.Where(f => !files.Contains(f)))
        {
            await target.DeletePhotoAsync(stale, cancellationToken);
        }

        LogPublished(logger, snapshot.Constats.Count, files.Count);
        return snapshot;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed publication must never stop the back end: the public page keeps the previous snapshot, the failure is shown in the administration and retried.")]
    private async Task<bool> TryPublishAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await PublishAsync(cancellationToken);
            status.Succeeded(snapshot.PublishedAt);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            status.Failed(time.GetUtcNow(), exception.GetType().Name);
            LogFailed(logger, exception);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Constats} constats and {Files} photo files.")]
    private static partial void LogPublished(ILogger logger, int constats, int files);

    [LoggerMessage(Level = LogLevel.Error, Message = "The publication failed; the previous snapshot stays online.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
