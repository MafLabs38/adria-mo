namespace AdriaMo.Core.Constats;

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
