namespace PcGamePreservationStudio.Core.Models;

public sealed record ArchiveBuildResult
{
    public required bool Success { get; init; }
    public required string ArchiveFolderPath { get; init; }
    public int TotalFiles { get; init; }
    public long TotalBytes { get; init; }
    public string? ErrorMessage { get; init; }
    public PreservationAssessment? Preservation { get; init; }

    /// <summary>1 for a folder-only archive; the number of DISC_NN folders otherwise.</summary>
    public int DiscCount { get; init; } = 1;

    /// <summary>Relative paths of files too large to fit on a single disc of the chosen medium — left in place, not assigned to any disc.</summary>
    public IReadOnlyList<string> FilesTooLargeForMedium { get; init; } = [];

    /// <summary>Set when <see cref="ArchiveBuildRequest.MediaType"/> is <see cref="Models.MediaType.IsoOnly"/> and ISO creation succeeded.
    /// When set, <see cref="ArchiveFolderPath"/>'s staged folder has been removed — this is the only remaining output.</summary>
    public string? IsoPath { get; init; }

    /// <summary>
    /// Set when <see cref="ArchiveBuildRequest.MediaType"/> is a physical optical-disc size (CD/DVD/BD) and
    /// each resulting DISC_NN\ folder was successfully converted to its own DISC_NN.iso, ready to hand to
    /// Burn Disc. A DISC_NN\ folder is only kept on disk (instead of being replaced by its .iso) if that
    /// disc's ISO build failed — see <see cref="IsoBuildWarning"/> in that case.
    /// </summary>
    public IReadOnlyList<string> DiscIsoPaths { get; init; } = [];

    /// <summary>Set when an ISO was requested but could not be built (backend not found, or the build failed) —
    /// the staged folder archive (or, for multi-disc, the affected DISC_NN\ folder) is kept intact as a
    /// fallback in this case.</summary>
    public string? IsoBuildWarning { get; init; }
}
