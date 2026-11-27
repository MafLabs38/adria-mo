using System.Net.Http.Json;
using AdriaMo.Api.Publishing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AdriaMo.Api.Test;

/// <summary>The publisher in the background, on a simulated clock: the gathering delay, the retries after a failure.</summary>
[Trait("Category", "Integration")]
public sealed class BackgroundPublisherTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changes_are_gathered_for_the_delay_then_published_once()
    {
        await using var factory = new ClockFactory();
        var admin = await factory.AdminAsync("rassemble");
        var status = factory.Services.GetRequiredService<PublicationStatus>();
        var started = await AdvanceUntilAsync(factory.Clock, () => status.Current.LastSuccessAt);

        var number = await ConstatApiTests.CreateAsync(admin);
        var requestedAt = factory.Clock.GetUtcNow();
        var published = await AdvanceUntilAsync(factory.Clock, () => status.Current.LastSuccessAt > started ? status.Current.LastSuccessAt : null);

        (published!.Value - requestedAt).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30), "changes are gathered for the delay");

        (await File.ReadAllTextAsync(Path.Join(factory.PublishFolder, SnapshotPublisher.SnapshotFile), Ct)).Should().Contain($"\"number\":{number},");
    }

    [Fact]
    public async Task A_failed_publication_keeps_the_previous_snapshot_and_is_tried_again()
    {
        await using var factory = new ClockFactory();
        var admin = await factory.AdminAsync("reessaie");
        var status = factory.Services.GetRequiredService<PublicationStatus>();
        await AdvanceUntilAsync(factory.Clock, () => status.Current.LastSuccessAt);
        var snapshot = Path.Join(factory.PublishFolder, SnapshotPublisher.SnapshotFile);
        var before = await File.ReadAllTextAsync(snapshot, Ct);

        // The target refuses to write: the publication fails, the public page keeps what it had.
        factory.Target.Failing = true;
        await ConstatApiTests.CreateAsync(admin);
        await AdvanceUntilAsync(factory.Clock, () => status.Current.LastError);
        (await File.ReadAllTextAsync(snapshot, Ct)).Should().Be(before);
        (await admin.GetFromJsonAsync<PublicationState>("/api/v1/publication", Ct))!.LastError.Should().Be(nameof(IOException));

        // Tried again after the retry delay, without any new change.
        factory.Target.Failing = false;
        await AdvanceUntilAsync(factory.Clock, () => status.Current.LastError is null ? status.Current.LastSuccessAt : null);
        (await File.ReadAllTextAsync(snapshot, Ct)).Should().NotBe(before);
    }

    /// <summary>
    /// Moves the simulated clock a second at a time until the background work, which runs on its own thread, gives a value: a few
    /// real seconds at most (generous for a loaded runner).
    /// </summary>
    private static async Task<T> AdvanceUntilAsync<T>(FakeTimeProvider clock, Func<T?> read)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var value = read();
        while (value is null)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, deadline.Token);
            value = read();
        }

        return value;
    }

    private sealed class ClockFactory : AdriaMoFactory
    {
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 27, 14, 0, 0, TimeSpan.Zero));

        protected override IDictionary<string, string?> Settings => new Dictionary<string, string?> { ["Publish:Delay"] = "00:00:30" };

        public FlakyTarget Target { get; private set; } = null!;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<FolderPublishTarget>();
                services.AddSingleton<IPublishTarget>(provider => Target = new FlakyTarget(provider.GetRequiredService<FolderPublishTarget>()));
            });
        }
    }

    /// <summary>The folder target, which fails on demand as a full disk or an unreachable R2 would.</summary>
    internal sealed class FlakyTarget(IPublishTarget inner) : IPublishTarget
    {
        public bool Failing { get; set; }

        public Task<IReadOnlySet<string>> ListPhotosAsync(CancellationToken cancellationToken) => inner.ListPhotosAsync(cancellationToken);

        public Task PutPhotoAsync(string name, string source, CancellationToken cancellationToken) =>
            Failing ? throw new IOException("disk full") : inner.PutPhotoAsync(name, source, cancellationToken);

        public Task PutSnapshotAsync(byte[] content, CancellationToken cancellationToken) =>
            Failing ? throw new IOException("disk full") : inner.PutSnapshotAsync(content, cancellationToken);

        public Task DeletePhotoAsync(string name, CancellationToken cancellationToken) => inner.DeletePhotoAsync(name, cancellationToken);
    }
}
