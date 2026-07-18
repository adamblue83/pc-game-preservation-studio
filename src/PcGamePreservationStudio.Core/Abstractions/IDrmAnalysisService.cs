using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Reports DRM/launcher evidence without inspecting or modifying protected executables. Phase 3, not yet implemented.</summary>
public interface IDrmAnalysisService
{
    Task<DrmAnalysisResult> AnalyzeAsync(GameLibraryEntry game, CancellationToken cancellationToken = default);
}
