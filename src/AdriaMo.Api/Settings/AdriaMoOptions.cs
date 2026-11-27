using System.ComponentModel.DataAnnotations;

namespace AdriaMo.Api.Settings;

/// <summary>Where the back end keeps its files (<c>ADRIAMO__STORAGE__*</c>).</summary>
internal sealed class StorageOptions
{
    /// <summary>The section name.</summary>
    public const string Section = "Storage";

    /// <summary>The file of the database, in the data folder.</summary>
    public const string DatabaseFile = "adria-mo.db";

    /// <summary>The folder of the keys that sign the session cookies, in the data folder.</summary>
    public const string KeysFolder = "keys";

    /// <summary>The private folder: the database and the photos, originals included. A volume, never published.</summary>
    [Required]
    public string DataFolder { get; set; } = "";

    /// <summary>The folder the public snapshot is written to (spec §10.2): what the static server or the CDN serves.</summary>
    [Required]
    public string PublishFolder { get; set; } = "";
}

/// <summary>The building (<c>ADRIAMO__BUILDING__*</c>).</summary>
internal sealed class BuildingOptions
{
    /// <summary>The section name.</summary>
    public const string Section = "Building";

    /// <summary>The IANA time zone of the building: the local time of undated EXIF data and the calendar of "ouvert depuis".</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Paris";
}

/// <summary>The accounts (<c>ADRIAMO__AUTH__*</c>, <c>ADRIAMO__BOOTSTRAP__*</c>).</summary>
internal sealed class AuthOptions
{
    /// <summary>The section name.</summary>
    public const string Section = "Auth";

    /// <summary>Login attempts per minute from one address.</summary>
    [Range(1, 1000)]
    public int LoginAttemptsPerMinute { get; set; } = 10;

    /// <summary>The user name of the super-admin created at first start, when there is none.</summary>
    public string? BootstrapUserName { get; set; }

    /// <summary>The file holding that super-admin's password (a mounted secret).</summary>
    public string? BootstrapPasswordFile { get; set; }
}

/// <summary>The publication of the public snapshot (<c>ADRIAMO__PUBLISH__*</c>).</summary>
internal sealed class PublishOptions
{
    /// <summary>The section name.</summary>
    public const string Section = "Publish";

    /// <summary>How long changes are gathered before one publication (spec §10.2).</summary>
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>The network in front of the back end (<c>ADRIAMO__NETWORK__*</c>).</summary>
internal sealed class NetworkOptions
{
    /// <summary>The section name.</summary>
    public const string Section = "Network";

    /// <summary>
    /// Whether the back end is reachable only through one reverse proxy, whose <c>X-Forwarded-For</c> gives the client's address
    /// (the limit of login attempts is per address). Off by default: the header is then ignored.
    /// </summary>
    public bool BehindProxy { get; set; }
}
