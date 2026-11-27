using System.Reflection;

namespace AdriaMo.Api;

/// <summary>What the API says about itself: its name and the version it was built with.</summary>
internal static class InfoEndpoints
{
    /// <summary>The information of this build.</summary>
    private static readonly AppInfo Current = new(
        "Adria MO",
        typeof(InfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");

    /// <summary>Maps <c>GET info</c> on the group.</summary>
    /// <param name="group">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapInfo(this RouteGroupBuilder group)
    {
        group.MapGet("/info", () => TypedResults.Ok(Current));
        return group;
    }
}

/// <summary>The name and the version of the application.</summary>
/// <param name="Name">The product name.</param>
/// <param name="Version">The version it was built with.</param>
internal sealed record AppInfo(string Name, string Version);
