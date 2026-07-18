using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Media capacity, safety-margin, and multi-disc fit calculations (Phase 4).</summary>
public interface IDiscCapacityService
{
    /// <summary>
    /// A single-number estimate: does an archive of this size fit on one disc of this medium?
    /// <paramref name="customCapacityBytes"/> is required for <see cref="MediaType.Custom"/>,
    /// <see cref="MediaType.Usb"/>, and <see cref="MediaType.ExternalDrive"/>, whose capacity isn't fixed.
    /// </summary>
    MediaCapacityPlan Plan(long archiveSizeBytes, MediaType mediaType, long? customCapacityBytes = null, long? safetyMarginOverrideBytes = null);

    /// <summary>
    /// Assigns files to one or more discs of the given medium without ever splitting a single file.
    /// Files that cannot fit on a single disc even alone are returned separately rather than assigned.
    /// </summary>
    MultiDiscPlan PlanMultiDisc(IReadOnlyList<PlannedFile> files, MediaType mediaType, long? customCapacityBytes = null, long? safetyMarginOverrideBytes = null);
}
