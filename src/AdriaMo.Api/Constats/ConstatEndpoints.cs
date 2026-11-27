using AdriaMo.Api.Accounts;
using AdriaMo.Api.Data;
using AdriaMo.Api.Photos;
using AdriaMo.Api.Publishing;
using AdriaMo.Core.Constats;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdriaMo.Api.Constats;

/// <summary>The constats and their pieces, in the administration (spec §3, §4).</summary>
internal static class ConstatEndpoints
{
    /// <summary>Maps <c>/constats/*</c>, <c>/categories</c> and <c>/photos/{name}</c>.</summary>
    /// <param name="api">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapConstats(this RouteGroupBuilder api)
    {
        var editor = api.MapGroup("/").RequireAuthorization(AccountsSetup.EditorPolicy);
        editor.MapGet("/categories", () => TypedResults.Ok(Categories.All));
        editor.MapGet("/constats", ListAsync);
        editor.MapPost("/constats", CreateAsync);
        editor.MapGet("/constats/{id:int}", GetAsync);
        editor.MapPut("/constats/{id:int}", UpdateAsync);
        editor.MapPost("/constats/{id:int}/pieces", AddPieceAsync).DisableAntiforgery();
        editor.MapGet("/photos/{name}", Photo);
        return api;
    }

    private static async Task<Ok<ConstatSummary[]>> ListAsync(AdriaMoDbContext db, CancellationToken cancellationToken)
    {
        var constats = await db.Constats.AsNoTracking().Include(c => c.Pieces).ToListAsync(cancellationToken);
        return TypedResults.Ok(constats
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => new ConstatSummary(
                c.Id,
                c.Title,
                c.Category,
                c.Location,
                ConstatStatusKey.Of(c.Status),
                c.IsPublic,
                c.FirstObservedAt,
                c.UpdatedAt,
                c.Pieces.Count,
                c.Pieces.OrderBy(p => p.ObservedAt).Select(p => p.Photo?.ThumbnailFileName).FirstOrDefault(n => n is not null)))
            .ToArray());
    }

    private static async Task<Results<Created<ConstatDetail>, ValidationProblem>> CreateAsync(
        ConstatRequest request,
        AdriaMoDbContext db,
        PublishQueue publish,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var now = time.GetUtcNow();
        var constat = new Constat
        {
            Title = request.Title!.Trim(),
            Category = request.Category!,
            Location = Blank(request.Location),
            IsPublic = request.IsPublic,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Constats.Add(constat);
        await db.SaveChangesAsync(cancellationToken);
        publish.Request();
        return TypedResults.Created($"/api/v1/constats/{constat.Id}", Detail(constat));
    }

    private static async Task<Results<Ok<ConstatDetail>, NotFound>> GetAsync(int id, AdriaMoDbContext db, CancellationToken cancellationToken)
    {
        var constat = await db.Constats.AsNoTracking().Include(c => c.Pieces).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return constat is null ? TypedResults.NotFound() : TypedResults.Ok(Detail(constat));
    }

    private static async Task<Results<Ok<ConstatDetail>, NotFound, ValidationProblem>> UpdateAsync(
        int id,
        ConstatRequest request,
        AdriaMoDbContext db,
        PublishQueue publish,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var constat = await db.Constats.Include(c => c.Pieces).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (constat is null)
        {
            return TypedResults.NotFound();
        }

        constat.Title = request.Title!.Trim();
        constat.Category = request.Category!;
        constat.Location = Blank(request.Location);
        constat.IsPublic = request.IsPublic;
        constat.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        publish.Request();
        return TypedResults.Ok(Detail(constat));
    }

    private static async Task<Results<Created<PieceDetail>, NotFound, ValidationProblem, Conflict<string>>> AddPieceAsync(
        int id,
        [FromForm] string? text,
        IFormFile? photo,
        [AsParameters] PieceServices services,
        CancellationToken cancellationToken)
    {
        var (db, intake, users, http, publish, time) = services;
        var comment = Blank(text);
        if (comment is null && photo is null)
        {
            return Invalid("piece", "Empty");
        }

        if (comment?.Length > Constat.TextMaxLength)
        {
            return Invalid("text", "TooLong");
        }

        if (photo is not null && (photo.Length == 0 || photo.Length > PhotoIntake.MaxBytes))
        {
            return Invalid("photo", "TooLarge");
        }

        var constat = await db.Constats.Include(c => c.Pieces).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (constat is null)
        {
            return TypedResults.NotFound();
        }

        IntakenPhoto? intaken = null;
        if (photo is not null)
        {
            using var buffer = new MemoryStream((int)photo.Length);
            await photo.CopyToAsync(buffer, cancellationToken);
            intaken = await intake.AcceptAsync(buffer.ToArray(), cancellationToken);
            if (intaken is null)
            {
                return Invalid("photo", "NotAnImage");
            }

            if (constat.Pieces.Any(p => p.Photo?.OriginalSha256 == intaken.Photo.OriginalSha256))
            {
                return TypedResults.Conflict("AlreadyAdded");
            }
        }

        var now = time.GetUtcNow();
        var piece = new Piece
        {
            ConstatId = constat.Id,
            Text = comment,
            TakenAt = intaken?.TakenAt,
            AddedAt = now,
            AuthorId = users.GetUserId(http.User)!,
            Photo = intaken?.Photo,
        };
        constat.Pieces.Add(piece);
        constat.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        publish.Request();
        return TypedResults.Created($"/api/v1/constats/{constat.Id}", PieceDetail.Of(piece));
    }

    private static Results<PhysicalFileHttpResult, NotFound> Photo(string name, PhotoIntake intake)
    {
        var path = intake.DerivedPath(name);
        return path is not null && File.Exists(path)
            ? TypedResults.PhysicalFile(path, "image/jpeg", enableRangeProcessing: false)
            : TypedResults.NotFound();
    }

    private static ValidationProblem? Validate(ConstatRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var title = request.Title?.Trim() ?? "";
        if (title.Length == 0)
        {
            errors["title"] = ["Required"];
        }
        else if (title.Length > Constat.TitleMaxLength)
        {
            errors["title"] = ["TooLong"];
        }

        if (Categories.Find(request.Category) is null)
        {
            errors["category"] = ["Unknown"];
        }

        if (request.Location?.Trim().Length > Constat.LocationMaxLength)
        {
            errors["location"] = ["TooLong"];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    private static ValidationProblem Invalid(string field, string code) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [code] });

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ConstatDetail Detail(Constat c) => new(
        c.Id,
        c.Title,
        c.Category,
        c.Location,
        ConstatStatusKey.Of(c.Status),
        c.IsPublic,
        c.FirstObservedAt,
        c.UpdatedAt,
        [.. c.Pieces.OrderBy(p => p.ObservedAt).ThenBy(p => p.Id).Select(PieceDetail.Of)]);
}

/// <summary>What adding a piece needs.</summary>
/// <param name="Db">The database.</param>
/// <param name="Intake">The photo intake.</param>
/// <param name="Users">The accounts, for the author.</param>
/// <param name="Http">The request.</param>
/// <param name="Publish">The publication.</param>
/// <param name="Time">The clock.</param>
internal sealed record PieceServices(
    AdriaMoDbContext Db,
    PhotoIntake Intake,
    UserManager<AdriaMoUser> Users,
    HttpContext Http,
    PublishQueue Publish,
    TimeProvider Time);

/// <summary>A new constat, or the new fields of one.</summary>
/// <param name="Title">The title.</param>
/// <param name="Category">The key of the category.</param>
/// <param name="Location">Where, or <c>null</c>.</param>
/// <param name="IsPublic">Whether the public page shows it.</param>
internal sealed record ConstatRequest(string? Title, string? Category, string? Location, bool IsPublic = true);

/// <summary>A constat in the list of the administration.</summary>
internal sealed record ConstatSummary(
    int Number,
    string Title,
    string Category,
    string? Location,
    string Status,
    bool IsPublic,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset UpdatedAt,
    int PieceCount,
    string? Thumbnail);

/// <summary>A constat with its pieces.</summary>
internal sealed record ConstatDetail(
    int Number,
    string Title,
    string Category,
    string? Location,
    string Status,
    bool IsPublic,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PieceDetail> Pieces);

/// <summary>A piece, in the administration.</summary>
internal sealed record PieceDetail(
    int Id,
    string? Text,
    DateTimeOffset? TakenAt,
    DateTimeOffset AddedAt,
    DateTimeOffset ObservedAt,
    string? Photo,
    string? Thumbnail,
    string? OriginalSha256)
{
    /// <summary>The detail of a piece.</summary>
    /// <param name="piece">The piece.</param>
    /// <returns>Its detail.</returns>
    public static PieceDetail Of(Piece piece) => new(
        piece.Id,
        piece.Text,
        piece.TakenAt,
        piece.AddedAt,
        piece.ObservedAt,
        piece.Photo?.PublishedFileName,
        piece.Photo?.ThumbnailFileName,
        piece.Photo?.OriginalSha256);
}
