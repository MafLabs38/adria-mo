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

/// <summary>Where a constat stands (spec §3). The follow-up vertical moves a constat along; until then it is open.</summary>
public enum ConstatStatus
{
    /// <summary>Seen, not yet reported.</summary>
    Open,

    /// <summary>Reported to the landlord.</summary>
    ReportedToLandlord,

    /// <summary>An intervention is planned.</summary>
    InterventionPlanned,

    /// <summary>Works are in progress.</summary>
    InProgress,

    /// <summary>Resolved, checked on the spot by an administrator.</summary>
    Resolved,

    /// <summary>Closed.</summary>
    Closed,

    /// <summary>Broken again after being resolved.</summary>
    Reopened,
}
