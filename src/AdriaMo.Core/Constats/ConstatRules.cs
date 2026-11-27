namespace AdriaMo.Core.Constats;

/// <summary>What a constat must hold (spec §3): the checks of a creation or an edit, by field and code.</summary>
public static class ConstatRules
{
    /// <summary>Checks the fields of a constat.</summary>
    /// <param name="title">The title.</param>
    /// <param name="category">The key of the category.</param>
    /// <param name="location">The location, or <c>null</c>.</param>
    /// <returns>The problems, by field (<c>title</c>, <c>category</c>, <c>location</c>); empty when the fields are accepted.</returns>
    public static IReadOnlyDictionary<string, string[]> Check(string? title, string? category, string? location)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmed = title?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors["title"] = ["Required"];
        }
        else if (trimmed.Length > Constat.TitleMaxLength)
        {
            errors["title"] = ["TooLong"];
        }

        if (Categories.Find(category) is null)
        {
            errors["category"] = ["Unknown"];
        }

        if (location?.Trim().Length > Constat.LocationMaxLength)
        {
            errors["location"] = ["TooLong"];
        }

        return errors;
    }

    /// <summary>Checks the comment of a piece.</summary>
    /// <param name="text">The comment, trimmed, or <c>null</c>.</param>
    /// <param name="hasPhoto">Whether the piece has a photo.</param>
    /// <returns>The problem, or <c>null</c>: a piece holds a photo or a comment, and a comment is not too long.</returns>
    public static FieldProblem? CheckPiece(string? text, bool hasPhoto)
    {
        if (string.IsNullOrWhiteSpace(text) && !hasPhoto)
        {
            return new FieldProblem("piece", "Empty");
        }

        return text?.Length > Constat.TextMaxLength ? new FieldProblem("text", "TooLong") : null;
    }
}

/// <summary>A problem of one field: the name of the field and the code the interface says in French.</summary>
/// <param name="Field">The field.</param>
/// <param name="Code">The code.</param>
public sealed record FieldProblem(string Field, string Code)
{
}
