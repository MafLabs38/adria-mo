namespace AdriaMo.Api.Settings;

/// <summary>Registers the typed settings, checked when the application starts.</summary>
internal static class SettingsSetup
{
    /// <summary>Binds and validates every section.</summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddAdriaMoSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<BuildingOptions>()
            .Bind(configuration.GetSection(BuildingOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Building:TimeZone is not a known time zone.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Section))
            .ValidateDataAnnotations()
            .Validate(
                o => string.IsNullOrWhiteSpace(o.BootstrapUserName) == string.IsNullOrWhiteSpace(o.BootstrapPasswordFile),
                "Auth:BootstrapUserName and Auth:BootstrapPasswordFile go together.")
            .ValidateOnStart();
        services.AddOptions<PublishOptions>().Bind(configuration.GetSection(PublishOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NetworkOptions>().Bind(configuration.GetSection(NetworkOptions.Section));
        return services;
    }
}
