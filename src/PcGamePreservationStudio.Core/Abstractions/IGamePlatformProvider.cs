using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Enumerates games visible from one source (Steam, GOG, local folder). The UI depends only on this abstraction, never on a specific platform's implementation.</summary>
public interface IGamePlatformProvider
{
    GamePlatform Platform { get; }

    Task<IReadOnlyList<GameLibraryEntry>> GetGamesAsync(CancellationToken cancellationToken = default);
}
