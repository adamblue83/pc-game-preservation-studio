using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Aggregates every registered <see cref="IGamePlatformProvider"/> into one unified library for the UI.</summary>
public interface IGameDetectionService
{
    Task<IReadOnlyList<GameLibraryEntry>> GetAllGamesAsync(CancellationToken cancellationToken = default);

    Task<GameDetail?> GetGameDetailAsync(Guid gameId, CancellationToken cancellationToken = default);
}
