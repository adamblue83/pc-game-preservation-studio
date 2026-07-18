namespace PcGamePreservationStudio.Core.Models;

/// <summary>
/// Final result of an archive/ISO/disc verification pass. Produced by
/// <c>IArchiveVerificationService</c>. A disc/archive is never marked
/// <see cref="Verified"/> merely because a burn or copy reported success —
/// verification always re-reads and re-hashes the files.
/// </summary>
public enum VerificationOutcome
{
    Verified,
    VerifiedWithWarnings,
    Failed,
    Incomplete,
}

public sealed record VerificationResult
{
    public required VerificationOutcome Outcome { get; init; }
    public required TimeSpan Duration { get; init; }
    public IReadOnlyList<string> MissingFiles { get; init; } = [];
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];
    public IReadOnlyList<string> UnreadableFiles { get; init; } = [];
    public IReadOnlyList<string> UnexpectedFiles { get; init; } = [];
}
