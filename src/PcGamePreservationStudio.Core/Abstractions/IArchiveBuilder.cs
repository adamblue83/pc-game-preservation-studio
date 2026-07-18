using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Collects, hashes, and packages a folder-based archive for a game (Phase 3).</summary>
public interface IArchiveBuilder
{
    Task<ArchiveBuildResult> BuildAsync(ArchiveBuildRequest request, IProgress<ArchiveBuildProgress>? progress = null, CancellationToken cancellationToken = default);
}
