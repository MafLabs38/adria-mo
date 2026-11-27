namespace AdriaMo.Core.Constats;

/// <summary>A category of constat: a stable key, stored and published, and its French label.</summary>
/// <param name="Key">The key, in lower case, never shown.</param>
/// <param name="Label">The label shown, in French.</param>
public sealed record Category(string Key, string Label);

/// <summary>The starting categories (spec §3). The administration will make them editable; the keys stay.</summary>
public static class Categories
{
    /// <summary>The categories, in the order they are offered.</summary>
    public static IReadOnlyList<Category> All { get; } =
    [
        new("peinture", "Peinture et finitions"),
        new("portes", "Portes et fermetures"),
        new("ascenseur", "Ascenseur"),
        new("eclairage", "Éclairage"),
        new("electricite", "Électricité"),
        new("proprete", "Propreté"),
        new("encombrants", "Encombrants"),
        new("poubelles", "Poubelles"),
        new("nuisibles", "Nuisibles"),
        new("humidite", "Humidité et fuites"),
        new("securite", "Sécurité et incendie"),
        new("degradations", "Dégradations"),
        new("exterieurs", "Extérieurs"),
    ];

    /// <summary>Finds a category by its key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The category, or <c>null</c> when the key is unknown.</returns>
    public static Category? Find(string? key) => All.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
}
