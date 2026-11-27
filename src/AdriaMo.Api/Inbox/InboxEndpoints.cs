using AdriaMo.Api.Accounts;
using AdriaMo.Api.Constats;
using AdriaMo.Api.Data;
using AdriaMo.Api.Photos;
using AdriaMo.Api.Publishing;
using AdriaMo.Core.Constats;
using AdriaMo.Core.Photos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdriaMo.Api.Inbox;

/// <summary>
/// The inbox "À classer" (spec §4.1): photos shared from the gallery or sent later by the phone wait there, sorted by the date
/// they were taken and grouped in series, until they are attached to a constat. It is shared by the administrators.
/// </summary>
internal static class InboxEndpoints
{
    /// <summary>The most photos attached at once.</summary>
    public const int MaxAttached = 200;

    /// <summary>SQLite's code of a broken constraint (the unique index of the photos).</summary>
    private const int UniqueViolation = 19;

    /// <summary>Maps <c>/inbox/*</c>.</summary>
    /// <param name="api">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapInbox(this RouteGroupBuilder api)
    {
        var inbox = api.MapGroup("/inbox").RequireAuthorization(AccountsSetup.EditorPolicy);
        inbox.MapGet("/", ListAsync);
        // No antiforgery token, as for the pieces of a constat: the X-Adria-Mo header and the SameSite=Strict cookie.
        inbox.MapPost("/", AddAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(PhotoUpload.RequestLimit));
        inbox.MapPost("/attach", AttachAsync);
        inbox.MapDelete("/{id:int}", DiscardAsync);
        return api;
    }

    private static async Task<Ok<InboxListing>> ListAsync(AdriaMoDbContext db, CancellationToken cancellationToken)
    {
        var photos = await db.Inbox.AsNoTracking().ToListAsync(cancellationToken);
        var authors = await AuthorsAsync(db, photos, cancellationToken);
        var sorted = InboxSorting.Of(photos, new PhotoSeriesGrouper());
        return TypedResults.Ok(new InboxListing(
            [.. sorted.Series.Select(s => new InboxSeries(s[0].TakenAt!.Value, s[^1].TakenAt!.Value, [.. s.Select(p => InboxItem.Of(p, authors))]))],
            [.. sorted.Undated.Select(p => InboxItem.Of(p, authors))]));
    }

    private static async Task<Results<Created<InboxItem>, Ok<InboxItem>, ValidationProblem, Conflict<string>>> AddAsync(
        IFormFile? photo,
        [AsParameters] InboxServices services,
        CancellationToken cancellationToken)
    {
        var (db, intake, users, http, time) = services;
        if (photo is null)
        {
            return ConstatEndpoints.Invalid("photo", "Required");
        }

        if (PhotoUpload.SizeProblem(photo) is { } size)
        {
            return ConstatEndpoints.Invalid("photo", size);
        }

        var content = await PhotoUpload.ReadAsync(photo, cancellationToken);
        var sha256 = PhotoIntake.Sha256(content);
        // Sent again (an answer lost on the way, the same photo shared twice): the photo that waits already is the answer.
        if (await FindAsync(db, sha256, cancellationToken) is { } waiting)
        {
            return TypedResults.Ok(InboxItem.Of(waiting, await AuthorsAsync(db, [waiting], cancellationToken)));
        }

        if (await db.Pieces.AnyAsync(p => p.Photo!.OriginalSha256 == sha256, cancellationToken))
        {
            return TypedResults.Conflict("AlreadyAdded");
        }

        var intaken = await intake.AcceptAsync(content, cancellationToken);
        if (intaken.Photo is null)
        {
            return ConstatEndpoints.Invalid("photo", intaken.Problem ?? "NotAnImage");
        }

        var added = new InboxPhoto
        {
            Photo = intaken.Photo.Photo,
            TakenAt = intaken.Photo.TakenAt,
            AddedAt = time.GetUtcNow(),
            AuthorId = users.GetUserId(http.User)!,
        };
        db.Inbox.Add(added);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: UniqueViolation })
        {
            // The same photo arrived twice at the same time: the unique index kept one, which is the answer.
            db.Entry(added).State = EntityState.Detached;
            var raced = await FindAsync(db, sha256, cancellationToken) ?? throw new InvalidOperationException("The photo could not be saved.", exception);
            return TypedResults.Ok(InboxItem.Of(raced, await AuthorsAsync(db, [raced], cancellationToken)));
        }

        return TypedResults.Created((string?)null, InboxItem.Of(added, await AuthorsAsync(db, [added], cancellationToken)));
    }

    private static async Task<Results<Ok<AttachResult>, NotFound, ValidationProblem>> AttachAsync(
        AttachRequest request,
        AdriaMoDbContext db,
        PublishQueue publish,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var ids = request.Photos!.Distinct().ToArray();
        var photos = await db.Inbox.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        if (photos.Count == 0)
        {
            return TypedResults.NotFound();
        }

        var now = time.GetUtcNow();
        // A new constat gets its number when saved: its pieces are linked through the navigation (EF sets their ConstatId).
        var constat = request.Create is { } create
            ? db.Constats.Add(ConstatEndpoints.NewConstat(create, now)).Entity
            : await db.Constats.Include(c => c.Pieces).FirstOrDefaultAsync(c => c.Id == request.Constat, cancellationToken);
        if (constat is null)
        {
            return TypedResults.NotFound();
        }

        // Oldest first; a photo the constat already holds leaves the inbox without a second piece. The comment goes with the first
        // photo added.
        var text = ConstatEndpoints.Blank(request.Text);
        var attached = 0;
        foreach (var photo in photos.OrderBy(p => p.ObservedAt).ThenBy(p => p.Id))
        {
            if (constat.Pieces.All(p => p.Photo?.OriginalSha256 != photo.Photo.OriginalSha256))
            {
                constat.Pieces.Add(photo.ToPiece(constat.Id, text));
                text = null;
                attached++;
            }

            db.Inbox.Remove(photo);
        }

        constat.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        publish.Request();
        return TypedResults.Ok(new AttachResult(constat.Id, attached, photos.Count - attached, ids.Length - photos.Count));
    }

    private static async Task<Results<NoContent, NotFound>> DiscardAsync(
        int id,
        AdriaMoDbContext db,
        PhotoIntake intake,
        ILogger<InboxPhoto> logger,
        CancellationToken cancellationToken)
    {
        var photo = await db.Inbox.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        db.Inbox.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        if (!await db.Pieces.AnyAsync(p => p.Photo!.OriginalSha256 == photo.Photo.OriginalSha256, cancellationToken)
            && !intake.Forget(photo.Photo))
        {
            logger.LogWarning("The files of the discarded photo {Sha256} could not all be deleted.", photo.Photo.OriginalSha256);
        }

        return TypedResults.NoContent();
    }

    private static ValidationProblem? Validate(AttachRequest request)
    {
        if (request.Photos is not { Length: > 0 } photos)
        {
            return ConstatEndpoints.Invalid("photos", "Required");
        }

        if (photos.Length > MaxAttached)
        {
            return ConstatEndpoints.Invalid("photos", "TooMany");
        }

        if ((request.Constat is null) == (request.Create is null))
        {
            return ConstatEndpoints.Invalid("constat", "Required");
        }

        if (request.Create is { } create && ConstatEndpoints.Validate(create) is { } invalid)
        {
            return invalid;
        }

        return ConstatRules.CheckPiece(ConstatEndpoints.Blank(request.Text), hasPhoto: true) is { } text
            ? ConstatEndpoints.Invalid(text.Field, text.Code)
            : null;
    }

    private static Task<InboxPhoto?> FindAsync(AdriaMoDbContext db, string sha256, CancellationToken cancellationToken) =>
        db.Inbox.AsNoTracking().FirstOrDefaultAsync(p => p.Photo.OriginalSha256 == sha256, cancellationToken);

    private static async Task<Dictionary<string, string>> AuthorsAsync(AdriaMoDbContext db, IEnumerable<InboxPhoto> photos, CancellationToken cancellationToken)
    {
        var ids = photos.Select(p => p.AuthorId).Distinct().ToArray();
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? "", cancellationToken);
    }
}

/// <summary>What adding a photo to the inbox needs.</summary>
/// <param name="Db">The database.</param>
/// <param name="Intake">The photo intake.</param>
/// <param name="Users">The accounts, for the author.</param>
/// <param name="Http">The request.</param>
/// <param name="Time">The clock.</param>
internal sealed record InboxServices(AdriaMoDbContext Db, PhotoIntake Intake, UserManager<AdriaMoUser> Users, HttpContext Http, TimeProvider Time);

/// <summary>The inbox: the series of dated photos, the most recent first, and the undated photos.</summary>
/// <param name="Series">The series.</param>
/// <param name="Undated">The undated photos.</param>
internal sealed record InboxListing(IReadOnlyList<InboxSeries> Series, IReadOnlyList<InboxItem> Undated);

/// <summary>Photos taken a few minutes apart.</summary>
/// <param name="StartedAt">When the first was taken.</param>
/// <param name="EndedAt">When the last was taken.</param>
/// <param name="Photos">The photos, oldest first.</param>
internal sealed record InboxSeries(DateTimeOffset StartedAt, DateTimeOffset EndedAt, IReadOnlyList<InboxItem> Photos);

/// <summary>A photo of the inbox.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Photo">The name of its reduced copy.</param>
/// <param name="Thumbnail">The name of its thumbnail.</param>
/// <param name="TakenAt">When it was taken, or <c>null</c>.</param>
/// <param name="AddedAt">When it was sent.</param>
/// <param name="Author">Who sent it.</param>
internal sealed record InboxItem(int Id, string Photo, string Thumbnail, DateTimeOffset? TakenAt, DateTimeOffset AddedAt, string Author)
{
    /// <summary>The item of a photo.</summary>
    /// <param name="photo">The photo.</param>
    /// <param name="authors">The user names by account.</param>
    /// <returns>The item.</returns>
    public static InboxItem Of(InboxPhoto photo, IReadOnlyDictionary<string, string> authors) => new(
        photo.Id,
        photo.Photo.PublishedFileName,
        photo.Photo.ThumbnailFileName,
        photo.TakenAt,
        photo.AddedAt,
        authors.GetValueOrDefault(photo.AuthorId, ""));
}

/// <summary>Photos of the inbox to attach to a constat: an existing one, or one created with them.</summary>
/// <param name="Photos">The photos.</param>
/// <param name="Constat">The number of an existing constat, or <c>null</c>.</param>
/// <param name="Create">The fields of a new constat, or <c>null</c>.</param>
/// <param name="Text">A comment that goes with the first photo, or <c>null</c>.</param>
internal sealed record AttachRequest(int[]? Photos, int? Constat, ConstatRequest? Create, string? Text);

/// <summary>What attaching did.</summary>
/// <param name="Constat">The number of the constat.</param>
/// <param name="Attached">The photos added to it.</param>
/// <param name="AlreadyThere">The photos it already held, removed from the inbox without a second piece.</param>
/// <param name="Missing">The photos no longer in the inbox (attached or discarded meanwhile by someone else).</param>
internal sealed record AttachResult(int Constat, int Attached, int AlreadyThere, int Missing);
