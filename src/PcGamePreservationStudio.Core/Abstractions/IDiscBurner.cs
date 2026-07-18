using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>
/// Burns an already-built ISO image (see <see cref="IIsoBuilder"/>, Phase 6) to optical media via
/// IMAPI2. Does not master a disc image directly from a folder — build an ISO first. See
/// docs/BURNING_BACKENDS.md for the risk assessment (Phase 7).
/// </summary>
public interface IDiscBurner
{
    Task<IReadOnlyList<OpticalDriveInfo>> GetOpticalDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>Progress is coarse (0.0 at start, 1.0 on completion) — IMAPI2's native write-progress
    /// events are not wired up; see docs/BURNING_BACKENDS.md for why.</summary>
    Task<DiscBurnResult> BurnIsoAsync(string isoPath, string driveId, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
