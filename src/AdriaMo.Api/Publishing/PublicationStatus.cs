namespace AdriaMo.Api.Publishing;

/// <summary>
/// The outcome of the last publications, shown in the administration: a failed publication leaves the public page on the previous
/// snapshot, and an administrator must be able to see it.
/// </summary>
internal sealed class PublicationStatus
{
    private readonly Lock _gate = new();
    private PublicationState _state = new(null, null, null);

    /// <summary>The current state.</summary>
    public PublicationState Current
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Records a publication that succeeded.</summary>
    /// <param name="at">When.</param>
    public void Succeeded(DateTimeOffset at)
    {
        lock (_gate)
        {
            _state = new PublicationState(at, at, null);
        }
    }

    /// <summary>Records a publication that failed.</summary>
    /// <param name="at">When.</param>
    /// <param name="reason">The kind of failure, without detail from the file system or the database.</param>
    public void Failed(DateTimeOffset at, string reason)
    {
        lock (_gate)
        {
            _state = _state with { LastAttemptAt = at, LastError = reason };
        }
    }
}

/// <summary>The state of the publication.</summary>
/// <param name="LastAttemptAt">The last attempt, or <c>null</c> before the first.</param>
/// <param name="LastSuccessAt">The last success: the date of what the public page shows.</param>
/// <param name="LastError">The kind of the last failure, or <c>null</c> when the last attempt succeeded.</param>
internal sealed record PublicationState(DateTimeOffset? LastAttemptAt, DateTimeOffset? LastSuccessAt, string? LastError);
