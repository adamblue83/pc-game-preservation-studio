using System.Text;
using System.Text.Json;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving;

/// <summary>Writes the Metadata\, Checksums\, and Restore\ files described in docs/ARCHIVE_FORMAT.md.</summary>
public static class ArchiveMetadataWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static PreservationAssessment AssessPreservation(GameLibraryEntry game, bool hasSaves, bool hasInstallerFiles)
    {
        var reasons = new List<string>();
        PreservationRating rating;

        if (game.Platform == GamePlatform.Gog && hasInstallerFiles)
        {
            reasons.Add("Official GOG offline installer files archived");
            reasons.Add("GOG offline installers are DRM-free and do not require Galaxy or an internet connection to install");
            reasons.Add(hasSaves ? "Save files included" : "No save files were included in this archive");
            reasons.Add("Checksums created for every installer file");
            reasons.Add("Restore instructions included");
            rating = PreservationRating.Excellent;
        }
        else if (game.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(game.ManifestPath))
        {
            reasons.Add("Complete installed files archived");
            reasons.Add("Steam manifest included");
            reasons.Add(hasSaves ? "Save files included" : "No save files were included in this archive");
            reasons.Add("Steam ownership may still be required to reinstall, update, or verify this game");
            reasons.Add("Offline launch has not been tested");
            rating = PreservationRating.Good;
        }
        else if (game.Platform == GamePlatform.Gog)
        {
            reasons.Add("Complete installed files archived");
            reasons.Add(hasSaves ? "Save files included" : "No save files were included in this archive");
            reasons.Add("This installation was detected from a local GOG registry entry, not an official offline installer");
            reasons.Add("GOG Galaxy may still be required to reinstall, update, or verify this game");
            reasons.Add("GOG primarily distributes DRM-free games, but DRM-free status has not been confirmed for this specific title");
            reasons.Add("Offline launch has not been tested");
            rating = PreservationRating.Good;
        }
        else if (game.Platform == GamePlatform.LocalFolder)
        {
            reasons.Add("Complete installed files archived");
            reasons.Add(hasSaves ? "Save files included" : "No save files were included in this archive");
            reasons.Add("No platform ownership record is associated with a manually-added local folder");
            rating = PreservationRating.Unknown;
        }
        else
        {
            reasons.Add("Complete installed files archived");
            reasons.Add("Insufficient evidence to assess platform requirements for this archive");
            rating = PreservationRating.Unknown;
        }

        return new PreservationAssessment { Rating = rating, Reasons = reasons };
    }

    public static async Task WriteAllAsync(
        string archiveRoot,
        ArchiveBuildRequest request,
        IReadOnlyList<CopiedFileRecord> copiedFiles,
        IReadOnlyList<SaveLocationCandidate> savedLocations,
        PreservationAssessment preservation,
        List<string> buildLog,
        CancellationToken cancellationToken)
    {
        var metadataDir = Path.Combine(archiveRoot, "Metadata");
        var checksumsDir = Path.Combine(archiveRoot, "Checksums");
        var restoreDir = Path.Combine(archiveRoot, "Restore");
        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(checksumsDir);
        Directory.CreateDirectory(restoreDir);

        var totalBytes = copiedFiles.Sum(f => f.SizeBytes);
        var createdUtc = DateTimeOffset.UtcNow;

        await WriteGameInfoAsync(metadataDir, request, createdUtc, cancellationToken);
        await WritePreservationReportAsync(metadataDir, preservation, cancellationToken);
        await WriteSaveLocationsAsync(metadataDir, savedLocations, cancellationToken);
        await WriteSourceFilesAsync(metadataDir, request, copiedFiles, totalBytes, cancellationToken);
        await WriteArchiveManifestAsync(metadataDir, request, totalBytes, createdUtc, preservation, cancellationToken);
        await WriteChecksumsAsync(checksumsDir, copiedFiles, cancellationToken);
        await WriteRestoreInstructionsAsync(restoreDir, request, preservation, cancellationToken);
        await WriteReadmeAsync(archiveRoot, request, createdUtc, preservation, cancellationToken);

        buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Metadata written to {metadataDir}");
    }

    private static async Task WriteGameInfoAsync(string metadataDir, ArchiveBuildRequest request, DateTimeOffset createdUtc, CancellationToken cancellationToken)
    {
        var game = request.Game;
        var info = new
        {
            title = game.Title,
            platform = game.Platform.ToString(),
            appId = game.PlatformAppId,
            installPath = game.InstallPath,
            manifestPath = game.ManifestPath,
            sizeOnDiskBytes = game.SizeOnDiskBytes,
            archiveCreatedUtc = createdUtc,
            notes = request.Notes,
        };

        await File.WriteAllTextAsync(Path.Combine(metadataDir, "game_info.json"), JsonSerializer.Serialize(info, JsonOptions), cancellationToken);

        var text = new StringBuilder()
            .AppendLine($"Title: {game.Title}")
            .AppendLine($"Platform: {game.Platform}")
            .AppendLine($"App ID: {game.PlatformAppId}")
            .AppendLine($"Install path: {game.InstallPath}")
            .AppendLine($"Manifest: {game.ManifestPath}")
            .AppendLine($"Archived: {createdUtc:u}");
        await File.WriteAllTextAsync(Path.Combine(metadataDir, "game_info.txt"), text.ToString(), cancellationToken);
    }

    private static async Task WritePreservationReportAsync(string metadataDir, PreservationAssessment preservation, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(metadataDir, "preservation_report.json"),
            JsonSerializer.Serialize(new { rating = preservation.Rating.ToString(), reasons = preservation.Reasons }, JsonOptions),
            cancellationToken);

        var text = new StringBuilder()
            .AppendLine($"Preservation rating: {preservation.Rating}")
            .AppendLine()
            .AppendLine("Reasons:");
        foreach (var reason in preservation.Reasons)
        {
            text.AppendLine($"- {reason}");
        }

        await File.WriteAllTextAsync(Path.Combine(metadataDir, "preservation_report.txt"), text.ToString(), cancellationToken);
    }

    private static async Task WriteSaveLocationsAsync(string metadataDir, IReadOnlyList<SaveLocationCandidate> savedLocations, CancellationToken cancellationToken)
    {
        var payload = savedLocations.Select(s => new
        {
            fullPath = s.FullPath,
            kind = s.Kind.ToString(),
            confidence = s.Confidence.ToString(),
            detectionReason = s.DetectionReason,
            archiveFolderName = s.ArchiveFolderName,
        });

        await File.WriteAllTextAsync(Path.Combine(metadataDir, "save_locations.json"), JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
    }

    private static async Task WriteSourceFilesAsync(string metadataDir, ArchiveBuildRequest request, IReadOnlyList<CopiedFileRecord> copiedFiles, long totalBytes, CancellationToken cancellationToken)
    {
        var payload = new
        {
            sourceInstallPath = request.Game.InstallPath,
            includedSaveLocationPaths = request.IncludedSaveLocationPaths,
            totalFiles = copiedFiles.Count,
            totalBytes,
        };

        await File.WriteAllTextAsync(Path.Combine(metadataDir, "source_files.json"), JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
    }

    private static async Task WriteArchiveManifestAsync(
        string metadataDir, ArchiveBuildRequest request, long archiveSizeBytes, DateTimeOffset createdUtc, PreservationAssessment preservation, CancellationToken cancellationToken)
    {
        var game = request.Game;
        var manifest = new
        {
            title = game.Title,
            platform = game.Platform.ToString(),
            appId = game.PlatformAppId,
            archiveCreatedUtc = createdUtc,
            sourceInstallPath = game.InstallPath,
            sourceSizeOnDiskBytes = game.SizeOnDiskBytes,
            archiveSizeBytes,
            mediaType = "FolderOnly",
            discNumber = 1,
            discCount = 1,
            hashAlgorithm = "SHA256",
            applicationVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0-dev",
            offlineTestResult = "Not tested",
            requiredLauncher = game.Platform == GamePlatform.Steam ? "Steam" : "Unknown",
            requiredAccountNotice = game.Platform == GamePlatform.Steam
                ? "Restoring this archive to a Steam library still requires an account that owns this game."
                : "This archive was added manually; no platform ownership record is tracked.",
            preservationRating = preservation.Rating.ToString(),
            preservationReasons = preservation.Reasons,
        };

        await File.WriteAllTextAsync(Path.Combine(metadataDir, "archive_manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
    }

    private static async Task WriteChecksumsAsync(string checksumsDir, IReadOnlyList<CopiedFileRecord> copiedFiles, CancellationToken cancellationToken)
    {
        var sumsLines = copiedFiles
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .Select(f => $"{f.Sha256}  {f.RelativePath}");
        await File.WriteAllLinesAsync(Path.Combine(checksumsDir, "SHA256SUMS.txt"), sumsLines, cancellationToken);

        var manifest = new
        {
            hashAlgorithm = "SHA256",
            generatedUtc = DateTimeOffset.UtcNow,
            files = copiedFiles
                .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
                .Select(f => new { relativePath = f.RelativePath, sha256 = f.Sha256, sizeBytes = f.SizeBytes }),
        };

        await File.WriteAllTextAsync(Path.Combine(checksumsDir, "verification_manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
    }

    private static async Task WriteRestoreInstructionsAsync(string restoreDir, ArchiveBuildRequest request, PreservationAssessment preservation, CancellationToken cancellationToken)
    {
        var game = request.Game;
        var text = new StringBuilder()
            .AppendLine("Restore instructions")
            .AppendLine("=====================")
            .AppendLine()
            .AppendLine("1. Verify this archive first (compare Checksums\\SHA256SUMS.txt against the files).")
            .AppendLine("2. Copy the contents of Game\\ to your chosen install location")
            .AppendLine(game.Platform == GamePlatform.Steam
                ? "   (typically a steamapps\\common\\<install folder> directory in a Steam library you own this game on)."
                : "   (a folder of your choice).")
            .AppendLine(game.Platform == GamePlatform.Steam
                ? "3. If Platform\\ contains a Steam appmanifest, only restore it after backing up any existing manifest — Steam may re-validate or update the installation afterward. Restoring files does not change your Steam account or grant ownership."
                : "3. This archive does not include or modify any platform account information.")
            .AppendLine("4. If Saves\\ is present, restore each subfolder to its original location — see Metadata\\save_locations.json for the recorded source paths.")
            .AppendLine("5. Never launch restored executables automatically; review them yourself first.")
            .AppendLine()
            .AppendLine($"Preservation rating: {preservation.Rating}");
        foreach (var reason in preservation.Reasons)
        {
            text.AppendLine($"- {reason}");
        }

        await File.WriteAllTextAsync(Path.Combine(restoreDir, "README.txt"), text.ToString(), cancellationToken);
    }

    private static async Task WriteReadmeAsync(string archiveRoot, ArchiveBuildRequest request, DateTimeOffset createdUtc, PreservationAssessment preservation, CancellationToken cancellationToken)
    {
        var text = new StringBuilder()
            .AppendLine("Personal Archival Copy")
            .AppendLine("Created from a legally owned installation")
            .AppendLine("Not for resale or distribution")
            .AppendLine()
            .AppendLine($"Game: {request.Game.Title}")
            .AppendLine($"Platform: {request.Game.Platform}")
            .AppendLine($"Archived: {createdUtc:u}")
            .AppendLine($"Preservation rating: {preservation.Rating}")
            .AppendLine()
            .AppendLine("See Metadata\\archive_manifest.json for full details and Restore\\README.txt for restore instructions.");

        await File.WriteAllTextAsync(Path.Combine(archiveRoot, "README.txt"), text.ToString(), cancellationToken);
    }
}
