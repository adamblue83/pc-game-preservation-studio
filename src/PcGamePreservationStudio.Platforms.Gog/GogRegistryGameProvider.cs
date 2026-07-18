using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Platforms.Gog;

/// <summary>
/// Detects locally installed GOG games via the registry keys GOG's own installers write —
/// HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\&lt;gameID&gt; — so GOG Galaxy doesn't need to be
/// installed or running. Never reads Galaxy's account/session data, and never automates login.
/// </summary>
public sealed class GogRegistryGameProvider(ILogger<GogRegistryGameProvider> logger) : IGogLibraryProvider
{
    private const string RegistryPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private const string RegistryPathFallback = @"SOFTWARE\GOG.com\Games";

    public GamePlatform Platform => GamePlatform.Gog;

    public Task<IReadOnlyList<GameLibraryEntry>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<GameLibraryEntry>();
        var seenGameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var registryPath in new[] { RegistryPath, RegistryPathFallback })
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadGamesFromRegistryPath(registryPath, entries, seenGameIds);
        }

        return Task.FromResult<IReadOnlyList<GameLibraryEntry>>(entries);
    }

    private void ReadGamesFromRegistryPath(string registryPath, List<GameLibraryEntry> entries, HashSet<string> seenGameIds)
    {
        RegistryKey? gamesKey = null;
        try
        {
            gamesKey = Registry.LocalMachine.OpenSubKey(registryPath);
            if (gamesKey is null)
            {
                return;
            }

            foreach (var gameId in gamesKey.GetSubKeyNames())
            {
                if (!seenGameIds.Add(gameId))
                {
                    continue;
                }

                using var gameKey = gamesKey.OpenSubKey(gameId);
                var entry = TryReadEntry(gameKey, gameId);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.LogWarning(ex, "Could not read GOG registry entries under {Path}", registryPath);
        }
        finally
        {
            gamesKey?.Dispose();
        }
    }

    private GameLibraryEntry? TryReadEntry(RegistryKey? gameKey, string gameId)
    {
        if (gameKey is null)
        {
            return null;
        }

        var title = gameKey.GetValue("gameName") as string;
        var installPath = gameKey.GetValue("path") as string;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(installPath))
        {
            logger.LogWarning("Skipped GOG registry entry {GameId} — missing gameName or path", gameId);
            return null;
        }

        var exeFile = gameKey.GetValue("exeFile") as string;

        return new GameLibraryEntry
        {
            Id = DeterministicGuid($"gog:{gameId}"),
            Platform = GamePlatform.Gog,
            Title = title,
            InstallPath = installPath,
            PlatformAppId = gameId,
            LibraryPath = Path.GetDirectoryName(installPath),
            ExecutableCandidates = string.IsNullOrWhiteSpace(exeFile) ? [] : [exeFile],
        };
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }
}
