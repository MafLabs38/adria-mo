using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdriaMo.Api.Test;

/// <summary>
/// The back end in memory, with its own data and publish folders and a super-admin created at start. The client speaks HTTPS so the
/// secure session cookie of production is the one tested.
/// </summary>
public class AdriaMoFactory : WebApplicationFactory<Program>
{
    public const string SuperAdmin = "chef";
    public const string SuperAdminPassword = "la loge du gardien";

    private readonly string _root;
    private readonly bool _keepFolders;

    /// <summary>A back end with folders of its own, deleted with it.</summary>
    public AdriaMoFactory()
        : this(Path.Join(Path.GetTempPath(), "adria-mo-test-" + Guid.NewGuid().ToString("N")), keepFolders: false)
    {
    }

    /// <summary>A back end on the given folders: a second one on the same folders is the first one restarted.</summary>
    /// <param name="root">The folder of its data and publish folders.</param>
    /// <param name="keepFolders">Whether the folders stay when it stops.</param>
    protected AdriaMoFactory(string root, bool keepFolders)
    {
        _root = root;
        _keepFolders = keepFolders;
    }

    public string DataFolder => Path.Join(_root, "data");

    public string PublishFolder => Path.Join(_root, "publish");

    /// <summary>Settings a test changes, applied over the defaults of the factory.</summary>
    protected virtual IDictionary<string, string?> Settings => new Dictionary<string, string?>();

    protected virtual string BootstrapPassword => SuperAdminPassword;

    public HttpClient Client()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Adria-Mo", "1");
        return client;
    }

    public async Task<HttpClient> LoggedInAsync(string userName, string password)
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return client;
    }

    public Task<HttpClient> SuperAdminAsync() => LoggedInAsync(SuperAdmin, SuperAdminPassword);

    /// <summary>An administrator whose password is already its own.</summary>
    public async Task<HttpClient> AdminAsync(string userName = "eric", string password = "la porte du hall b")
    {
        var chef = await SuperAdminAsync();
        var ct = TestContext.Current.CancellationToken;
        (await chef.PostAsJsonAsync("/api/v1/users", new { userName, temporaryPassword = "provisoire du chef" }, ct)).EnsureSuccessStatusCode();
        var admin = await LoggedInAsync(userName, "provisoire du chef");
        (await admin.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "provisoire du chef", newPassword = password }, ct))
            .EnsureSuccessStatusCode();
        return admin;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        var passwordFile = Path.Join(_root, "super-admin-password");
        File.WriteAllText(passwordFile, BootstrapPassword + "\n");

        builder.UseSetting("Storage:DataFolder", DataFolder);
        builder.UseSetting("Storage:PublishFolder", PublishFolder);
        builder.UseSetting("Auth:BootstrapUserName", SuperAdmin);
        builder.UseSetting("Auth:BootstrapPasswordFile", passwordFile);
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
        builder.UseSetting("Publish:Delay", "00:00:00");
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && !_keepFolders && Directory.Exists(_root))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_root, recursive: true);
        }
    }
}
