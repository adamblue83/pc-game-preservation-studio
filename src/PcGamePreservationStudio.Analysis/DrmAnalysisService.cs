using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Analysis;

/// <summary>
/// Reports DRM/launcher evidence found by name-matching known marker files in a game's install
/// folder. This never opens, executes, disassembles, or otherwise inspects the contents of any
/// executable — it only checks whether specific, publicly-documented file names exist, and reports
/// what that does and doesn't tell you. It never asserts that a game "has DRM" or "will run offline";
/// every result is a confidence-qualified label plus the specific evidence behind it.
/// </summary>
public sealed class DrmAnalysisService(ILogger<DrmAnalysisService> logger) : IDrmAnalysisService
{
    // Each marker maps a known file name (case-insensitive) to the launcher/DRM system it evidences.
    private static readonly IReadOnlyDictionary<string, string> SteamworksMarkers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["steam_api.dll"] = "Steamworks SDK (32-bit)",
        ["steam_api64.dll"] = "Steamworks SDK (64-bit)",
        ["steam_appid.txt"] = "Steamworks app-id marker",
    };

    private static readonly IReadOnlyDictionary<string, string> ThirdPartyLauncherMarkers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["eacore.dll"] = "EA Desktop / Origin",
        ["origin.ini"] = "EA Desktop / Origin",
        ["ubisoftgamelauncher.exe"] = "Ubisoft Connect",
        ["uplay_r1_loader64.dll"] = "Ubisoft Connect",
        ["uplay_r1_loader.dll"] = "Ubisoft Connect",
        ["eossdk-win64-shipping.dll"] = "Epic Online Services",
        ["eossdk-win32-shipping.dll"] = "Epic Online Services",
        ["xlive.dll"] = "Games for Windows Live (legacy)",
        ["socialclub.dll"] = "Rockstar Games Social Club",
        ["battle.net.dll"] = "Battle.net",
    };

    public Task<DrmAnalysisResult> AnalyzeAsync(GameLibraryEntry game, CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(game, cancellationToken), cancellationToken);

    private DrmAnalysisResult Analyze(GameLibraryEntry game, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(game.InstallPath))
        {
            return new DrmAnalysisResult
            {
                Label = DrmAnalysisLabel.Unknown,
                Confidence = DrmAnalysisConfidence.Low,
                Findings =
                [
                    new DrmEvidenceFinding
                    {
                        Description = "Install folder not found",
                        Explanation = "The recorded install path no longer exists on disk, so no on-disk evidence could be gathered.",
                    },
                ],
            };
        }

        var foundSteamworks = new List<string>();
        var foundThirdPartyLaunchers = new List<string>();

        foreach (var fileName in EnumerateShallowFileNames(game.InstallPath, cancellationToken))
        {
            if (SteamworksMarkers.TryGetValue(fileName, out var steamSystem) && !foundSteamworks.Contains(steamSystem))
            {
                foundSteamworks.Add(steamSystem);
            }

            if (ThirdPartyLauncherMarkers.TryGetValue(fileName, out var launcherSystem) && !foundThirdPartyLaunchers.Contains(launcherSystem))
            {
                foundThirdPartyLaunchers.Add(launcherSystem);
            }
        }

        var findings = new List<DrmEvidenceFinding>();
        DrmAnalysisLabel label;
        DrmAnalysisConfidence confidence;

        if (foundThirdPartyLaunchers.Count > 0)
        {
            foreach (var system in foundThirdPartyLaunchers)
            {
                findings.Add(new DrmEvidenceFinding
                {
                    Description = $"Found files associated with {system}",
                    Explanation = $"This game likely requires {system} to be installed (and possibly running) to launch, independent of where it was purchased.",
                });
            }

            label = DrmAnalysisLabel.ThirdPartyLauncherLikelyRequired;
            confidence = foundThirdPartyLaunchers.Count > 1 ? DrmAnalysisConfidence.High : DrmAnalysisConfidence.Medium;
        }
        else if (foundSteamworks.Count > 0)
        {
            foreach (var system in foundSteamworks)
            {
                findings.Add(new DrmEvidenceFinding
                {
                    Description = $"Found {system}",
                    Explanation = "This game uses the Steamworks SDK, which typically requires the Steam client to be installed and running to launch, even if no other DRM is present.",
                });
            }

            label = DrmAnalysisLabel.PlatformClientLikelyRequired;
            confidence = DrmAnalysisConfidence.Medium;
        }
        else if (game.Platform == GamePlatform.Gog)
        {
            findings.Add(new DrmEvidenceFinding
            {
                Description = "No known launcher or online-activation files found",
                Explanation = "GOG primarily distributes DRM-free games. No Steamworks or third-party launcher files were found in the install folder, which is consistent with (but doesn't by itself prove) a DRM-free build.",
            });

            label = DrmAnalysisLabel.LikelyDrmFree;
            confidence = DrmAnalysisConfidence.Medium;
        }
        else
        {
            findings.Add(new DrmEvidenceFinding
            {
                Description = "No known launcher or DRM marker files found",
                Explanation = "This scan only checks for specific, publicly-documented file names and cannot detect every form of DRM or copy protection. Test the archived copy offline before relying on it.",
            });

            label = DrmAnalysisLabel.Unknown;
            confidence = DrmAnalysisConfidence.Low;
        }

        logger.LogInformation(
            "DRM analysis for {InstallPath} concluded {Label} ({Confidence})",
            PathRedactor.Redact(game.InstallPath),
            label,
            confidence);

        return new DrmAnalysisResult { Label = label, Confidence = confidence, Findings = findings };
    }

    /// <summary>
    /// Yields distinct file names found directly in <paramref name="rootPath"/> and its immediate
    /// subdirectories (not a full recursive walk) — launcher marker files are conventionally placed
    /// at the game root or one level down (e.g. a Binaries/bin folder), and bounding the scan depth
    /// keeps this fast on very large game installs.
    /// </summary>
    private IEnumerable<string> EnumerateShallowFileNames(string rootPath, CancellationToken cancellationToken)
    {
        foreach (var fileName in EnumerateFileNamesInDirectory(rootPath))
        {
            yield return fileName;
        }

        IReadOnlyList<string> subdirectories;
        try
        {
            subdirectories = Directory.GetDirectories(rootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            logger.LogWarning(ex, "Could not enumerate subdirectories of {Path} for DRM analysis", PathRedactor.Redact(rootPath));
            yield break;
        }

        foreach (var subdirectory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var fileName in EnumerateFileNamesInDirectory(subdirectory))
            {
                yield return fileName;
            }
        }
    }

    private IEnumerable<string> EnumerateFileNamesInDirectory(string directoryPath)
    {
        IReadOnlyList<string> files;
        try
        {
            files = Directory.GetFiles(directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            logger.LogWarning(ex, "Could not enumerate files in {Path} for DRM analysis", PathRedactor.Redact(directoryPath));
            yield break;
        }

        foreach (var file in files)
        {
            yield return Path.GetFileName(file);
        }
    }
}
