using AdriaMo.Api;
using AdriaMo.Api.Accounts;
using AdriaMo.Api.Constats;
using AdriaMo.Api.Data;
using AdriaMo.Api.Inbox;
using AdriaMo.Api.Photos;
using AdriaMo.Api.Publishing;
using AdriaMo.Api.Security;
using AdriaMo.Api.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
// Settings come from appsettings.json, then from the environment with the ADRIAMO__ prefix (ADRIAMO__SECTION__KEY).
builder.Configuration.AddEnvironmentVariables(prefix: "ADRIAMO__");

builder.Services.AddAdriaMoSettings(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
// The keys that sign the session cookies live in the data folder: a restart (an update) keeps the sessions, and above all
// the paired phones (spec §4.1), which could not log in again without a computer.
builder.Services.AddDataProtection().SetApplicationName("adria-mo");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<IOptions<StorageOptions>, ILoggerFactory>((keys, storage, logs) => keys.XmlRepository =
        new FileSystemXmlRepository(new DirectoryInfo(Path.Join(storage.Value.DataFolder, StorageOptions.KeysFolder)), logs));
builder.Services.AddDbContext<AdriaMoDbContext>((services, options) =>
{
    var folder = services.GetRequiredService<IOptions<StorageOptions>>().Value.DataFolder;
    options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Join(folder, StorageOptions.DatabaseFile) }.ToString());
});
// Hosted services start in this order: the migration first, then the accounts, then the publisher.
builder.Services.AddHostedService<DatabaseMigrator>();
builder.Services.AddAdriaMoAccounts();
builder.Services.AddSingleton<PhotoIntake>();
builder.Services.AddSingleton<PublishQueue>();
builder.Services.AddSingleton<PublicationStatus>();
builder.Services.AddSingleton<PublisherSettings>();
builder.Services.AddSingleton<IPublishTarget, FolderPublishTarget>();
builder.Services.AddSingleton<SnapshotPublisher>();
builder.Services.AddHostedService(services => services.GetRequiredService<SnapshotPublisher>());
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Only one proxy, the one in front of the back end (Network:BehindProxy).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
// Small requests everywhere; the endpoint that takes a photo allows more (ConstatEndpoints).
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 1024 * 1024);

var app = builder.Build();

app.UseExceptionHandler();
if (app.Services.GetRequiredService<IOptions<NetworkOptions>>().Value.BehindProxy)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<AppHeaderMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapGroup("/api/v1")
    .MapInfo()
    .MapAccounts()
    .MapUsers()
    .MapDevices()
    .MapConstats()
    .MapInbox();

await app.RunAsync();

/// <summary>The entry point, visible to the integration tests (WebApplicationFactory).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "WebApplicationFactory<Program> needs a public type to start the host; it is not a utility class.")]
public partial class Program;
