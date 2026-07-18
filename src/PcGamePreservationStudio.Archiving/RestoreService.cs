using System.Text.Json;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving;

/// <summary>
/// Restores a flat, single-root archive (a folder-only build, or a mounted ISO/disc root) back to
/// disk. Always re-verifies checksums before copying anything — see docs/ARCHIVE_FORMAT.md and
/// docs/ARCHITECTURE.md for the archive layout this reads. Multi-disc DISC_NN\ archives are not
/// stitched back together automatically; point restore at one disc's folder directly if needed.
/// </summary>
public sealed class RestoreService(IArchiveVerificationService verificationService, ILogger<RestoreService> logger) : IRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<RestorePreflightResult> PreflightAsync(string archiveSourcePath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(archiveSourcePath))
        {
            return new RestorePreflightResult { Success = false, ErrorMessage = $"Archive folder not found: {archiveSourcePath}" };
        }

        var manifestPath = Path.Combine(archiveSourcePath, "Metadata", "archive_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new RestorePreflightResult { Success = false, ErrorMessage = "This doesn't look like an archive created by this app — Metadata\\archive_manifest.json was not found." };
        }

        ArchiveManifestInfo manifest;
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var dto = JsonSerializer.Deserialize<ArchiveManifestDto>(manifestJson, JsonOptions)
                ?? throw new JsonException("archive_manifest.json deserialized to null.");
            manifest = new ArchiveManifestInfo
            {
                Title = dto.Title ?? "Unknown",
                Platform = dto.Platform ?? "Unknown",
                AppId = dto.AppId,
                ArchiveCreatedUtc = dto.ArchiveCreatedUtc,
                SourceInstallPath = dto.SourceInstallPath,
                ArchiveSizeBytes = dto.ArchiveSizeBytes,
                RequiredLauncher = dto.RequiredLauncher,
                RequiredAccountNotice = dto.RequiredAccountNotice,
                PreservationRating = dto.PreservationRating,
                PreservationReasons = dto.PreservationReasons ?? [],
            };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse {Path}", manifestPath);
            return new RestorePreflightResult { Success = false, ErrorMessage = "Metadata\\archive_manifest.json could not be read — it may be corrupted." };
        }

        var saveLocations = await ReadSaveLocationsAsync(archiveSourcePath, cancellationToken);

        return new RestorePreflightResult
        {
            Success = true,
            Manifest = manifest,
            SaveLocations = saveLocations,
            HasGameFiles = Directory.Exists(Path.Combine(archiveSourcePath, "Game")),
            HasInstallerFiles = Directory.Exists(Path.Combine(archiveSourcePath, "Installers")),
            HasPlatformFiles = Directory.Exists(Path.Combine(archiveSourcePath, "Platform")),
        };
    }

    private async Task<IReadOnlyList<RestoreSaveLocationInfo>> ReadSaveLocationsAsync(string archiveSourcePath, CancellationToken cancellationToken)
    {
        var saveLocationsPath = Path.Combine(archiveSourcePath, "Metadata", "save_locations.json");
        if (!File.Exists(saveLocationsPath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(saveLocationsPath, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<SaveLocationDto>>(json, JsonOptions) ?? [];
            return entries
                .Where(e => !string.IsNullOrWhiteSpace(e.ArchiveFolderName))
                .Select(e => new RestoreSaveLocationInfo
                {
                    ArchiveFolderName = e.ArchiveFolderName!,
                    OriginalFullPath = e.FullPath ?? "",
                    Kind = e.Kind ?? "Save",
                    Confidence = e.Confidence ?? "Unknown",
                    DetectionReason = e.DetectionReason,
                })
                .ToList();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse {Path}", saveLocationsPath);
            return [];
        }
    }

    public async Task<RestoreResult> RestoreAsync(RestoreRequest request, IProgress<RestoreProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(request.ArchiveSourcePath))
        {
            return new RestoreResult { Success = false, ErrorMessage = $"Archive folder not found: {request.ArchiveSourcePath}" };
        }

        progress?.Report(new RestoreProgress { Stage = RestoreStage.Verifying });
        var verification = await verificationService.VerifyAsync(request.ArchiveSourcePath, cancellationToken);
        if (verification.Outcome is VerificationOutcome.Failed or VerificationOutcome.Incomplete)
        {
            progress?.Report(new RestoreProgress { Stage = RestoreStage.Failed });
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = $"Archive verification did not pass ({verification.Outcome}) — nothing was restored.",
                VerificationResult = verification,
            };
        }

        var gameDir = Path.Combine(request.ArchiveSourcePath, "Game");
        var installersDir = Path.Combine(request.ArchiveSourcePath, "Installers");
        var platformDir = Path.Combine(request.ArchiveSourcePath, "Platform");
        var hasGameFiles = Directory.Exists(gameDir);
        var hasInstallerFiles = Directory.Exists(installersDir);
        var hasPlatformFiles = Directory.Exists(platformDir);

        if ((hasGameFiles || hasInstallerFiles) && string.IsNullOrWhiteSpace(request.InstallDestinationPath))
        {
            return new RestoreResult { Success = false, ErrorMessage = "An install destination is required to restore this archive's game files.", VerificationResult = verification };
        }

        var filesRestored = 0;
        var skipped = new List<string>();
        var notes = new List<string>();

        try
        {
            if (hasGameFiles)
            {
                progress?.Report(new RestoreProgress { Stage = RestoreStage.CopyingGameFiles });
                filesRestored += CopyDirectoryContents(gameDir, request.InstallDestinationPath!, request.OverwriteExistingFiles, skipped, progress, RestoreStage.CopyingGameFiles, cancellationToken);
            }

            if (hasInstallerFiles)
            {
                progress?.Report(new RestoreProgress { Stage = RestoreStage.CopyingInstallerFiles });
                filesRestored += CopyDirectoryContents(installersDir, request.InstallDestinationPath!, request.OverwriteExistingFiles, skipped, progress, RestoreStage.CopyingInstallerFiles, cancellationToken);
                notes.Add("Offline installer files were restored — run the installer yourself to (re)install the game; this app never runs installers automatically.");
            }

            if (hasPlatformFiles)
            {
                progress?.Report(new RestoreProgress { Stage = RestoreStage.CopyingPlatformFiles });
                var platformDestDir = Path.Combine(request.InstallDestinationPath!, "PlatformFiles");
                filesRestored += CopyDirectoryContents(platformDir, platformDestDir, request.OverwriteExistingFiles, skipped, progress, RestoreStage.CopyingPlatformFiles, cancellationToken);
                notes.Add($"Platform files (e.g. a Steam manifest) were copied to {platformDestDir} — move them into your platform library's own folder structure yourself if you want it to recognize this installation. Restoring files does not grant or transfer ownership.");
            }

            if (request.SaveLocationsToRestore.Count > 0)
            {
                progress?.Report(new RestoreProgress { Stage = RestoreStage.CopyingSaves });
                foreach (var selection in request.SaveLocationsToRestore)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var saveSourceDir = Path.Combine(request.ArchiveSourcePath, "Saves", selection.ArchiveFolderName);
                    if (!Directory.Exists(saveSourceDir))
                    {
                        logger.LogWarning("Save location folder not found in archive: {Path}", saveSourceDir);
                        continue;
                    }

                    filesRestored += CopyDirectoryContents(saveSourceDir, selection.RestoreToPath, request.OverwriteExistingFiles, skipped, progress, RestoreStage.CopyingSaves, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new RestoreProgress { Stage = RestoreStage.Failed });
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Restore failed for {ArchiveSourcePath}", request.ArchiveSourcePath);
            progress?.Report(new RestoreProgress { Stage = RestoreStage.Failed });
            return new RestoreResult { Success = false, ErrorMessage = ex.Message, VerificationResult = verification };
        }

        progress?.Report(new RestoreProgress { Stage = RestoreStage.Completed });
        return new RestoreResult
        {
            Success = true,
            VerificationResult = verification,
            FilesRestored = filesRestored,
            SkippedExistingFiles = skipped,
            Notes = notes,
        };
    }

    private static int CopyDirectoryContents(
        string sourceDir, string destDir, bool overwrite, List<string> skipped,
        IProgress<RestoreProgress>? progress, RestoreStage stage, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);
        var copied = 0;

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(destDir, relativePath);

            if (File.Exists(destFile) && !overwrite)
            {
                skipped.Add(destFile);
                continue;
            }

            progress?.Report(new RestoreProgress { Stage = stage, CurrentItem = relativePath });
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(sourceFile, destFile, overwrite: true);
            copied++;
        }

        return copied;
    }

    private sealed class ArchiveManifestDto
    {
        public string? Title { get; set; }
        public string? Platform { get; set; }
        public string? AppId { get; set; }
        public DateTimeOffset ArchiveCreatedUtc { get; set; }
        public string? SourceInstallPath { get; set; }
        public long ArchiveSizeBytes { get; set; }
        public string? RequiredLauncher { get; set; }
        public string? RequiredAccountNotice { get; set; }
        public string? PreservationRating { get; set; }
        public List<string>? PreservationReasons { get; set; }
    }

    private sealed class SaveLocationDto
    {
        public string? FullPath { get; set; }
        public string? Kind { get; set; }
        public string? Confidence { get; set; }
        public string? DetectionReason { get; set; }
        public string? ArchiveFolderName { get; set; }
    }
}
