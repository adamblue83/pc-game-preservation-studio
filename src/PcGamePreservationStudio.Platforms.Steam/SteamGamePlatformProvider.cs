using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Platforms.Steam;

/// <summary>Detects locally installed Steam games. Reads only libraryfolders.vdf and appmanifest_*.acf; never touches Steam credentials or automates login.</summary>
public sealed class SteamGamePlatformProvider(ISettingsService settingsService, ILogger<SteamGamePlatformProvider> logger) : IGamePlatformProvider
{
    public GamePlatform Platform => GamePlatform.Steam;

    public async Task<IReadOnlyList<GameLibraryEntry>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        var steamPath = SteamInstallLocator.Locate(settings.SteamInstallPathOverride);
        if (steamPath is null)
        {
            logger.LogInformation("Steam installation was not found");
            return [];
        }

        var libraryPaths = new List<string> { steamPath };

        var libraryFoldersVdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersVdfPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(libraryFoldersVdfPath, cancellationToken);
                libraryPaths.AddRange(SteamVdfParser.ParseLibraryFolders(content).Select(f => f.Path));
            }
            catch (Exception ex) when (ex is IOException or FormatException)
            {
                logger.LogWarning(ex, "Failed to parse {Path}", PathRedactor.Redact(libraryFoldersVdfPath));
            }
        }

        var entries = new List<GameLibraryEntry>();
        var seenLibraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryPath in libraryPaths)
        {
            if (!seenLibraryPaths.Add(libraryPath))
            {
                continue;
            }

            var steamAppsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsDir))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                    var manifest = SteamVdfParser.ParseAppManifest(content, libraryPath, manifestPath);
                    if (manifest is null)
                    {
                        logger.LogWarning("Manifest {Path} was missing required fields and was skipped", PathRedactor.Redact(manifestPath));
                        continue;
                    }

                    entries.Add(new GameLibraryEntry
                    {
                        Id = DeterministicGuid($"steam:{manifest.AppId}"),
                        Platform = GamePlatform.Steam,
                        Title = manifest.Name,
                        InstallPath = Path.Combine(steamAppsDir, "common", manifest.InstallDir),
                        SizeOnDiskBytes = manifest.SizeOnDiskBytes,
                        PlatformAppId = manifest.AppId,
                        ManifestPath = manifest.ManifestPath,
                        LibraryPath = manifest.LibraryPath,
                    });
                }
                catch (Exception ex) when (ex is IOException or FormatException)
                {
                    logger.LogWarning(ex, "Failed to parse manifest {Path}", PathRedactor.Redact(manifestPath));
                }
            }
        }

        return entries;
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }
}
