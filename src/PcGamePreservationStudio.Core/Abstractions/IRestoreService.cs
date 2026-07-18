using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>
/// Restores a previously built archive (a folder-only archive, or a mounted ISO/disc root) back to
/// disk. Only supports flat, single-root archives — a multi-disc archive's DISC_NN\ folders are not
/// stitched back together automatically (Phase 8). Never proceeds past a failed verification pass.
/// </summary>
public interface IRestoreService
{
    /// <summary>Reads Metadata\archive_manifest.json and Metadata\save_locations.json — fast, never hashes files.</summary>
    Task<RestorePreflightResult> PreflightAsync(string archiveSourcePath, CancellationToken cancellationToken = default);

    /// <summary>Always re-verifies the archive against its checksums first; restores nothing if that fails.</summary>
    Task<RestoreResult> RestoreAsync(RestoreRequest request, IProgress<RestoreProgress>? progress = null, CancellationToken cancellationToken = default);
}
