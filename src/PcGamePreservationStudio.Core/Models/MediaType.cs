namespace PcGamePreservationStudio.Core.Models;

public enum MediaType
{
    Cd700,
    Dvd5,
    Dvd9,
    Bd25,
    Bd50,
    Bd100,
    Bd128,
    Custom,
    Usb,
    ExternalDrive,
    FolderOnly,
    IsoOnly,
}

/// <summary>
/// Result of fitting an archive against a medium's capacity. Produced by
/// <c>IDiscCapacityService.Plan</c> (Phase 4).
/// </summary>
public sealed record MediaCapacityPlan
{
    public required MediaType MediaType { get; init; }
    public required long AdvertisedCapacityBytes { get; init; }
    public required long UsableCapacityBytes { get; init; }
    public required long SafetyMarginBytes { get; init; }
    public required long ArchiveSizeBytes { get; init; }
    public long RemainingBytes => UsableCapacityBytes - SafetyMarginBytes - ArchiveSizeBytes;
    public bool FitsOnSingleMedium => RemainingBytes >= 0;
}
