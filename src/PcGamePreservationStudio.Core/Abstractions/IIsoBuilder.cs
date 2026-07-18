using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>
/// Builds a UDF ISO image from a staged folder, via a detected external backend (e.g. oscdimg.exe).
/// Never bundles the backend itself — only detects a user-installed copy and reports clearly when
/// one isn't found (Phase 6).
/// </summary>
public interface IIsoBuilder
{
    /// <summary>Checks whether a usable backend is present, without attempting a build.</summary>
    IsoBackendAvailability GetAvailability(string? oscdimgPathOverride = null);

    /// <summary>Never throws for a missing/failed backend — reports failure via <see cref="IsoBuildResult"/> instead.</summary>
    Task<IsoBuildResult> BuildIsoAsync(string sourceFolder, string outputIsoPath, string? oscdimgPathOverride = null, CancellationToken cancellationToken = default);
}
