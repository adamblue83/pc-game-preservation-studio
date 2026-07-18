using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Finds likely save/configuration locations for review before copying. Phase 3, not yet implemented.</summary>
public interface ISaveDetectionService
{
    Task<IReadOnlyList<SaveLocationCandidate>> DetectSaveLocationsAsync(GameLibraryEntry game, CancellationToken cancellationToken = default);
}
