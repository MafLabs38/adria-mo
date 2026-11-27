using AdriaMo.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Data;

/// <summary>
/// Brings the database to the current schema at start, before the server accepts requests. Registered first: the account
/// bootstrap and the publisher, registered after it, rely on a migrated database.
/// </summary>
/// <param name="services">The root services.</param>
/// <param name="storage">The storage settings.</param>
internal sealed class DatabaseMigrator(IServiceProvider services, IOptions<StorageOptions> storage) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storage.Value.DataFolder);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdriaMoDbContext>().Database.MigrateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
