namespace AdriaMo.Core.Accounts;

/// <summary>The roles of the administration (spec §2). A visitor has no account.</summary>
public static class Roles
{
    /// <summary>Everything an administrator does, plus accounts and settings.</summary>
    public const string SuperAdmin = "super-admin";

    /// <summary>Everything on the content: constats, pieces, publication.</summary>
    public const string Admin = "admin";

    /// <summary>Both roles, in the order they are created.</summary>
    public static IReadOnlyList<string> All { get; } = [SuperAdmin, Admin];
}
