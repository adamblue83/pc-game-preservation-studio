using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.Services;

/// <summary>Aggregates every registered <see cref="IGamePlatformProvider"/> into one unified library.</summary>
public sealed class GameDetectionService(IEnumerable<IGamePlatformProvider> providers, ISettingsService settingsService) : IGameDetectionService
{
    public async Task<IReadOnlyList<GameLibraryEntry>> GetAllGamesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);

        var activeProviders = settings.SkipPlatformDetection
            ? providers.Where(p => p.Platform == GamePlatform.LocalFolder)
            : providers;

        var results = new List<GameLibraryEntry>();
        foreach (var provider in activeProviders)
        {
            results.AddRange(await provider.GetGamesAsync(cancellationToken));
        }

        return results;
    }

    public async Task<GameDetail?> GetGameDetailAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var entry = (await GetAllGamesAsync(cancellationToken)).FirstOrDefault(e => e.Id == gameId);
        return entry is null ? null : new GameDetail { Entry = entry };
    }
}
