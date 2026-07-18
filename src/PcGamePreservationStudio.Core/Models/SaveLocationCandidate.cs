namespace PcGamePreservationStudio.Core.Models;

public enum SaveLocationConfidence
{
    Low,
    Medium,
    High,
}

public enum SaveLocationKind
{
    Save,
    Configuration,
    Screenshot,
    Unrelated,
}

/// <summary>
/// A candidate save/configuration location surfaced to the user for review before copying.
/// Used by <c>ISaveDetectionService</c> (Phase 3, not yet implemented).
/// </summary>
public sealed record SaveLocationCandidate
{
    public required string FullPath { get; init; }
    public long? EstimatedSizeBytes { get; init; }
    public required string DetectionReason { get; init; }
    public required SaveLocationConfidence Confidence { get; init; }
    public SaveLocationKind Kind { get; init; } = SaveLocationKind.Save;
    public bool HasPrivacyWarning { get; init; }

    /// <summary>The Saves\&lt;name&gt;\ folder this was copied into, once included in a build. Null before then.</summary>
    public string? ArchiveFolderName { get; init; }
}
