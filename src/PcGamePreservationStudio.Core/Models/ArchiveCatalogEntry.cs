namespace PcGamePreservationStudio.Core.Models;

public enum ArchiveDestinationType
{
    Folder,
    Iso,
    OpticalDisc,
    Usb,
    ExternalDrive,
}

public enum ArchiveStatus
{
    Draft,
    Building,
    Completed,
    Failed,
}

/// <summary>A persisted row in the archive catalog. Populated for real starting Phase 3.</summary>
public sealed record ArchiveCatalogEntry
{
    public required Guid Id { get; init; }
    public required string GameTitle { get; init; }
    public required GamePlatform Platform { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public ArchiveDestinationType DestinationType { get; init; } = ArchiveDestinationType.Folder;
    public ArchiveStatus Status { get; init; } = ArchiveStatus.Draft;
    public string? ArchiveLocation { get; init; }
    public string? Notes { get; init; }
}
