namespace AdriaMo.Api.Security;

/// <summary>
/// Refuses every request that changes something without the header <c>X-Adria-Mo</c> (spec §2.1), whatever its path or the case
/// of its path. A page of another site cannot send a custom header without the browser asking this site first, which it refuses
/// (no CORS): on top of the <c>SameSite=Strict</c> cookie, a forged form or image cannot act on behalf of a logged-in
/// administrator, even from a page of the same site.
/// </summary>
/// <param name="next">The next middleware.</param>
internal sealed class AppHeaderMiddleware(RequestDelegate next)
{
    /// <summary>The header the administration sends with every request.</summary>
    public const string Header = "X-Adria-Mo";

    /// <summary>Runs the check.</summary>
    /// <param name="context">The request.</param>
    /// <returns>The task.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var changes = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
        if (changes && !context.Request.Headers.ContainsKey(Header))
        {
            return Results.Problem(
                    $"The header {Header} is required.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Requête refusée")
                .ExecuteAsync(context);
        }

        return next(context);
    }
}
