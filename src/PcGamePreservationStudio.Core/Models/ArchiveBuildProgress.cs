namespace PcGamePreservationStudio.Core.Models;

public enum ArchiveBuildStage
{
    CollectingFiles,
    CopyingFiles,
    HashingFiles,
    WritingMetadata,
    BuildingIso,
    Verifying,
    Completed,
    Failed,
}

public sealed record ArchiveBuildProgress
{
    public required ArchiveBuildStage Stage { get; init; }
    public string? CurrentItem { get; init; }
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
}
