namespace PcGamePreservationStudio.Core.Models;

/// <summary>A file being planned for disc placement — no content, just what capacity planning needs.</summary>
public sealed record PlannedFile(string RelativePath, long SizeBytes);

/// <summary>The files assigned to one disc in a multi-disc plan.</summary>
public sealed record DiscAssignment
{
    public required int DiscNumber { get; init; }
    public required IReadOnlyList<PlannedFile> Files { get; init; }
    public required long TotalBytes { get; init; }
}

/// <summary>
/// Result of fitting a set of files onto one or more discs of a given medium. Produced by
/// <c>IDiscCapacityService.PlanMultiDisc</c> (Phase 4). Never splits a single file across discs.
/// </summary>
public sealed record MultiDiscPlan
{
    public required MediaType MediaType { get; init; }
    public required long SafetyMarginBytes { get; init; }
    public required IReadOnlyList<DiscAssignment> Discs { get; init; }

    /// <summary>Files too large to fit on a single disc of this medium even alone — cannot be placed without multipart compression or a larger medium.</summary>
    public required IReadOnlyList<PlannedFile> FilesExceedingSingleDiscCapacity { get; init; }

    public int DiscCount => Discs.Count;
    public bool RequiresMultipleDiscs => Discs.Count > 1;
    public bool HasBlockingOversizedFiles => FilesExceedingSingleDiscCapacity.Count > 0;
}
