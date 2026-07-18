namespace PcGamePreservationStudio.Core.Models;

/// <summary>An optical drive, as reported by <c>IDiscBurner</c> (Phase 7).</summary>
public sealed record OpticalDriveInfo
{
    public required string DriveId { get; init; }
    public required string DriveLetter { get; init; }
    public bool IsMediaPresent { get; init; }
    public MediaType? DetectedMediaType { get; init; }
    public bool IsWritable { get; init; }
}
