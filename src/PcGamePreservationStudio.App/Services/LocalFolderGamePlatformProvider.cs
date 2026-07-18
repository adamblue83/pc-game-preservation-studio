using System.Security.Cryptography;
using System.Text;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.Services;

/// <summary>Exposes user-added local-folder sources as games alongside auto-detected Steam/GOG libraries.</summary>
public sealed class LocalFolderGamePlatformProvider(ILocalFolderSourceRepository repository) : IGamePlatformProvider
{
    public GamePlatform Platform => GamePlatform.LocalFolder;

    public async Task<IReadOnlyList<GameLibraryEntry>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await repository.GetAllAsync(cancellationToken);

        return sources.Select(source => new GameLibraryEntry
        {
            Id = DeterministicGuid($"local:{source.Id}"),
            Platform = GamePlatform.LocalFolder,
            Title = source.DisplayName,
            InstallPath = source.Path,
        }).ToList();
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }
}
