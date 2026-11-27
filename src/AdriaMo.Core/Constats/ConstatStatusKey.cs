using System.Text.Json;

namespace AdriaMo.Core.Constats;

/// <summary>The key of a status, as the API and the snapshot write it: "open", "reported-to-landlord".</summary>
public static class ConstatStatusKey
{
    /// <summary>The key of a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>Its key, in kebab case.</returns>
    public static string Of(ConstatStatus status) => JsonNamingPolicy.KebabCaseLower.ConvertName(status.ToString());
}
