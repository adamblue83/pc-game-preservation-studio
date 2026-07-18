using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving;

public sealed record SaveSearchRoot(string Root, string Label, SaveLocationConfidence Confidence);

/// <summary>
/// Surfaces likely save/configuration locations for user review. Never copies anything itself —
/// callers decide what to include based on the returned candidates.
/// </summary>
public sealed class SaveDetectionService(ILogger<SaveDetectionService> logger, IReadOnlyList<SaveSearchRoot>? searchRootsOverride = null) : ISaveDetectionService
{
    public Task<IReadOnlyList<SaveLocationCandidate>> DetectSaveLocationsAsync(GameLibraryEntry game, CancellationToken cancellationToken = default)
    {
        var installDirName = Path.GetFileName(game.InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var candidates = new List<SaveLocationCandidate>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchRoot in searchRootsOverride ?? GetDefaultSearchRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAddCandidate(candidates, seenPaths, searchRoot.Root, installDirName, searchRoot.Label, searchRoot.Confidence, exactMatch: true);
            TryAddCandidate(candidates, seenPaths, searchRoot.Root, game.Title, searchRoot.Label, DemoteConfidence(searchRoot.Confidence), exactMatch: false);
        }

        if (game.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(game.PlatformAppId))
        {
            AddSteamUserdataCandidates(candidates, seenPaths, game.PlatformAppId);
        }

        return Task.FromResult<IReadOnlyList<SaveLocationCandidate>>(candidates);
    }

    private static IReadOnlyList<SaveSearchRoot> GetDefaultSearchRoots()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return
        [
            new SaveSearchRoot(Path.Combine(documents, "My Games"), "Documents\\My Games", SaveLocationConfidence.High),
            new SaveSearchRoot(Path.Combine(userProfile, "Saved Games"), "Saved Games", SaveLocationConfidence.High),
            new SaveSearchRoot(appData, "%APPDATA%", SaveLocationConfidence.High),
            new SaveSearchRoot(localAppData, "%LOCALAPPDATA%", SaveLocationConfidence.High),
            new SaveSearchRoot(documents, "Documents", SaveLocationConfidence.Medium),
            new SaveSearchRoot(programData, "%PROGRAMDATA%", SaveLocationConfidence.Medium),
        ];
    }

    private static SaveLocationConfidence DemoteConfidence(SaveLocationConfidence confidence) => confidence switch
    {
        SaveLocationConfidence.High => SaveLocationConfidence.Medium,
        _ => SaveLocationConfidence.Low,
    };

    private void TryAddCandidate(
        List<SaveLocationCandidate> candidates,
        HashSet<string> seenPaths,
        string root,
        string folderNameToMatch,
        string rootLabel,
        SaveLocationConfidence confidence,
        bool exactMatch)
    {
        if (string.IsNullOrWhiteSpace(folderNameToMatch))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            var candidatePath = Path.Combine(root, folderNameToMatch);
            if (!Directory.Exists(candidatePath) || !seenPaths.Add(candidatePath))
            {
                return;
            }

            candidates.Add(new SaveLocationCandidate
            {
                FullPath = candidatePath,
                EstimatedSizeBytes = TryGetDirectorySize(candidatePath),
                DetectionReason = exactMatch
                    ? $"Folder name matches the game's install directory under {rootLabel}"
                    : $"Folder name matches the game's title under {rootLabel}",
                Confidence = confidence,
                Kind = SaveLocationKind.Save,
                HasPrivacyWarning = true,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not inspect potential save location under {Root}", rootLabel);
        }
    }

    private void AddSteamUserdataCandidates(List<SaveLocationCandidate> candidates, HashSet<string> seenPaths, string appId)
    {
        var steamRoot = LocateSteamRoot();
        if (steamRoot is null)
        {
            return;
        }

        var userdataDir = Path.Combine(steamRoot, "userdata");
        if (!Directory.Exists(userdataDir))
        {
            return;
        }

        try
        {
            foreach (var userDir in Directory.EnumerateDirectories(userdataDir))
            {
                var appSaveDir = Path.Combine(userDir, appId);
                if (!Directory.Exists(appSaveDir) || !seenPaths.Add(appSaveDir))
                {
                    continue;
                }

                candidates.Add(new SaveLocationCandidate
                {
                    FullPath = appSaveDir,
                    EstimatedSizeBytes = TryGetDirectorySize(appSaveDir),
                    DetectionReason = "Steam Cloud userdata folder for this App ID",
                    Confidence = SaveLocationConfidence.High,
                    Kind = SaveLocationKind.Save,
                    HasPrivacyWarning = true,
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not enumerate Steam userdata folder");
        }
    }

    private static string? LocateSteamRoot()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var fromRegistry = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(fromRegistry) && Directory.Exists(fromRegistry))
            {
                return fromRegistry;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // fall through to default paths
        }

        string[] defaultPaths = [@"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam"];
        return defaultPaths.FirstOrDefault(Directory.Exists);
    }

    private long? TryGetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not compute size for {Path}", path);
            return null;
        }
    }
}
