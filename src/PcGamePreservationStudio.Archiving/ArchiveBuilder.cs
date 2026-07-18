using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Archiving;

public sealed record CopiedFileRecord(string RelativePath, string AbsolutePath, long SizeBytes, string Sha256);

/// <summary>
/// Builds a folder-based archive: copies the game's files (and any selected save locations and
/// platform manifest), hashes everything with SHA-256, and writes the metadata described in
/// docs/ARCHIVE_FORMAT.md. When a disc-based <see cref="ArchiveBuildRequest.MediaType"/> is
/// requested, the result is then split into DISC_01\, DISC_02\, ... folders. Read-only against the
/// source — never touches or executes source files.
/// </summary>
public sealed class ArchiveBuilder(ILogger<ArchiveBuilder> logger, IDiscCapacityService discCapacityService, IIsoBuilder isoBuilder) : IArchiveBuilder
{
    public async Task<ArchiveBuildResult> BuildAsync(ArchiveBuildRequest request, IProgress<ArchiveBuildProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var archiveRoot = Path.Combine(request.DestinationFolder, ArchiveFolderNaming.ToArchiveFolderName(request.Game.Title));
        var buildLog = new List<string> { $"[{DateTimeOffset.UtcNow:O}] Starting archive build for '{request.Game.Title}' -> {archiveRoot}" };

        try
        {
            Directory.CreateDirectory(archiveRoot);

            progress?.Report(new ArchiveBuildProgress { Stage = ArchiveBuildStage.CollectingFiles });
            var copiedFiles = new List<CopiedFileRecord>();

            // A GOG-offline-installer-only archive has no installed-game directory to copy — just
            // the installer files below, which go into Installers\ instead of Game\.
            if (!string.IsNullOrWhiteSpace(request.Game.InstallPath) && Directory.Exists(request.Game.InstallPath))
            {
                var sourceFiles = CollectSourceFiles(request.Game.InstallPath, buildLog);
                var gameFilesDestRoot = Path.Combine(archiveRoot, "Game");
                await CopyAndHashFilesAsync(
                    request.Game.InstallPath, gameFilesDestRoot, sourceFiles, "Game",
                    copiedFiles, buildLog, progress, sourceFiles.Count, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(request.Game.ManifestPath) && File.Exists(request.Game.ManifestPath))
            {
                await CopyManifestAsync(request.Game.ManifestPath, archiveRoot, copiedFiles, buildLog, cancellationToken);
            }

            if (request.InstallerFilePaths.Count > 0)
            {
                await CopyInstallerFilesAsync(request.InstallerFilePaths, archiveRoot, copiedFiles, buildLog, progress, cancellationToken);
            }

            var savedLocations = new List<SaveLocationCandidate>();
            foreach (var savePath in request.IncludedSaveLocationPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(savePath))
                {
                    buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Skipped save location (no longer exists): {PathRedactor.Redact(savePath)}");
                    continue;
                }

                var saveFolderName = Path.GetFileName(savePath.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : "Save";
                var saveDestRoot = Path.Combine(archiveRoot, "Saves", saveFolderName);
                var saveFiles = CollectSourceFiles(savePath, buildLog);
                await CopyAndHashFilesAsync(
                    savePath, saveDestRoot, saveFiles, $"Saves/{saveFolderName}",
                    copiedFiles, buildLog, progress, saveFiles.Count, cancellationToken);

                savedLocations.Add(new SaveLocationCandidate
                {
                    FullPath = savePath,
                    Kind = SaveLocationKind.Save,
                    Confidence = SaveLocationConfidence.High,
                    DetectionReason = "Included by user for this archive",
                    ArchiveFolderName = saveFolderName,
                });
            }

            progress?.Report(new ArchiveBuildProgress { Stage = ArchiveBuildStage.WritingMetadata });
            var preservation = ArchiveMetadataWriter.AssessPreservation(request.Game, savedLocations.Count > 0, request.InstallerFilePaths.Count > 0);
            await ArchiveMetadataWriter.WriteAllAsync(archiveRoot, request, copiedFiles, savedLocations, preservation, buildLog, cancellationToken);

            var discCount = 1;
            IReadOnlyList<string> filesTooLargeForMedium = [];
            string? isoPath = null;
            string? isoBuildWarning = null;
            IReadOnlyList<string> discIsoPaths = [];

            if (request.MediaType == MediaType.IsoOnly)
            {
                // The build log must be on disk (inside the ISO) before the staged folder can be
                // removed, so write it now rather than after the ISO step like the other paths do.
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Archive build completed: {copiedFiles.Count} files, {copiedFiles.Sum(f => f.SizeBytes)} bytes");
                await File.AppendAllLinesAsync(Path.Combine(archiveRoot, "Metadata", "build_log.txt"), buildLog, cancellationToken);

                progress?.Report(new ArchiveBuildProgress { Stage = ArchiveBuildStage.BuildingIso });
                var outputIsoPath = archiveRoot + ".iso";
                var isoResult = await isoBuilder.BuildIsoAsync(archiveRoot, outputIsoPath, request.OscdimgPathOverride, cancellationToken);

                if (isoResult.Success && isoResult.IsoPath is { } builtIsoPath && File.Exists(builtIsoPath) && new FileInfo(builtIsoPath).Length > 0)
                {
                    isoPath = builtIsoPath;
                    try
                    {
                        Directory.Delete(archiveRoot, recursive: true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Best-effort cleanup; the ISO is already valid and is the authoritative output either way.
                        logger.LogWarning(ex, "ISO created but could not remove staged folder {Path}", PathRedactor.Redact(archiveRoot));
                    }
                }
                else
                {
                    isoBuildWarning = isoResult.ErrorMessage ?? "The ISO could not be built. The folder archive was kept instead.";
                    logger.LogWarning("ISO creation failed for {Game}: {Warning}", request.Game.Title, isoBuildWarning);
                }
            }
            else
            {
                if (request.MediaType != MediaType.FolderOnly)
                {
                    var splitResult = await ArchiveDiscSplitter.SplitAsync(archiveRoot, request, copiedFiles, discCapacityService, buildLog, cancellationToken);
                    discCount = splitResult.DiscCount;
                    filesTooLargeForMedium = splitResult.FilesTooLargeForMedium;

                    if (IsOpticalDiscMediaType(request.MediaType))
                    {
                        progress?.Report(new ArchiveBuildProgress { Stage = ArchiveBuildStage.BuildingIso });
                        var discIsoResult = await BuildDiscIsosAsync(archiveRoot, discCount, request.OscdimgPathOverride, buildLog, cancellationToken);
                        discIsoPaths = discIsoResult.IsoPaths;
                        isoBuildWarning = discIsoResult.Warning;
                    }
                }

                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Archive build completed: {copiedFiles.Count} files, {copiedFiles.Sum(f => f.SizeBytes)} bytes");
                await File.AppendAllLinesAsync(Path.Combine(archiveRoot, "Metadata", "build_log.txt"), buildLog, cancellationToken);
            }

            progress?.Report(new ArchiveBuildProgress { Stage = ArchiveBuildStage.Completed, FilesProcessed = copiedFiles.Count, TotalFiles = copiedFiles.Count });

            return new ArchiveBuildResult
            {
                Success = true,
                ArchiveFolderPath = archiveRoot,
                TotalFiles = copiedFiles.Count,
                TotalBytes = copiedFiles.Sum(f => f.SizeBytes),
                Preservation = preservation,
                DiscCount = discCount,
                FilesTooLargeForMedium = filesTooLargeForMedium,
                IsoPath = isoPath,
                DiscIsoPaths = discIsoPaths,
                IsoBuildWarning = isoBuildWarning,
            };
        }
        catch (OperationCanceledException)
        {
            buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Build cancelled by user");
            await TryWriteBuildLogAsync(archiveRoot, buildLog);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Archive build failed for {Game}", request.Game.Title);
            buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Build failed: {ex.Message}");
            await TryWriteBuildLogAsync(archiveRoot, buildLog);

            return new ArchiveBuildResult
            {
                Success = false,
                ArchiveFolderPath = archiveRoot,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static bool IsOpticalDiscMediaType(MediaType mediaType) =>
        mediaType is MediaType.Cd700 or MediaType.Dvd5 or MediaType.Dvd9 or MediaType.Bd25 or MediaType.Bd50 or MediaType.Bd100 or MediaType.Bd128;

    /// <summary>
    /// Converts each DISC_NN\ folder under <paramref name="archiveRoot"/> into its own DISC_NN.iso via
    /// <see cref="isoBuilder"/>, so a multi-disc archive is ready to hand straight to Burn Disc instead of
    /// leaving the caller to convert each disc's folder into an ISO themselves. A disc whose ISO build fails
    /// keeps its folder on disk as a fallback (mirroring the single-disc IsoOnly behavior) rather than losing
    /// that disc's files; other discs still get their ISOs.
    /// </summary>
    private async Task<(IReadOnlyList<string> IsoPaths, string? Warning)> BuildDiscIsosAsync(
        string archiveRoot, int discCount, string? oscdimgPathOverride, List<string> buildLog, CancellationToken cancellationToken)
    {
        var isoPaths = new List<string>();
        var warnings = new List<string>();

        for (var discNumber = 1; discNumber <= discCount; discNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discFolderName = $"DISC_{discNumber:00}";
            var discFolder = Path.Combine(archiveRoot, discFolderName);
            if (!Directory.Exists(discFolder))
            {
                continue;
            }

            var outputIsoPath = discFolder + ".iso";
            var isoResult = await isoBuilder.BuildIsoAsync(discFolder, outputIsoPath, oscdimgPathOverride, cancellationToken);

            if (isoResult.Success && isoResult.IsoPath is { } builtIsoPath && File.Exists(builtIsoPath) && new FileInfo(builtIsoPath).Length > 0)
            {
                isoPaths.Add(builtIsoPath);
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Built {discFolderName}.iso");
                try
                {
                    Directory.Delete(discFolder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup; the ISO is already valid and is the authoritative output either way.
                    logger.LogWarning(ex, "{Disc} ISO created but could not remove its staged folder {Path}", discFolderName, PathRedactor.Redact(discFolder));
                }
            }
            else
            {
                var warning = isoResult.ErrorMessage ?? $"{discFolderName}'s ISO could not be built. Its folder was kept instead.";
                warnings.Add($"{discFolderName}: {warning}");
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Warning: {warning}");
                logger.LogWarning("ISO creation failed for {Disc}: {Warning}", discFolderName, warning);
            }
        }

        return (isoPaths, warnings.Count > 0 ? string.Join(" ", warnings) : null);
    }

    private static async Task TryWriteBuildLogAsync(string archiveRoot, List<string> buildLog)
    {
        try
        {
            var metadataDir = Path.Combine(archiveRoot, "Metadata");
            Directory.CreateDirectory(metadataDir);
            await File.AppendAllLinesAsync(Path.Combine(metadataDir, "build_log.txt"), buildLog);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort log write; the caller already has the failure reason.
        }
    }

    private List<string> CollectSourceFiles(string sourceRoot, List<string> buildLog)
    {
        try
        {
            return [.. Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            logger.LogWarning(ex, "Could not fully enumerate {Path}", PathRedactor.Redact(sourceRoot));
            buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Warning: could not fully enumerate {PathRedactor.Redact(sourceRoot)}: {ex.Message}");
            return [];
        }
    }

    private async Task CopyAndHashFilesAsync(
        string sourceRoot,
        string destRoot,
        IReadOnlyList<string> sourceFiles,
        string archiveRelativePrefix,
        List<CopiedFileRecord> copiedFiles,
        List<string> buildLog,
        IProgress<ArchiveBuildProgress>? progress,
        int totalFilesForThisStage,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destRoot);
        var processed = 0;

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            var relativeToSourceRoot = Path.GetRelativePath(sourceRoot, sourceFile);
            var destFile = Path.Combine(destRoot, relativeToSourceRoot);

            progress?.Report(new ArchiveBuildProgress
            {
                Stage = ArchiveBuildStage.CopyingFiles,
                CurrentItem = relativeToSourceRoot,
                FilesProcessed = processed,
                TotalFiles = totalFilesForThisStage,
            });

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(sourceFile, destFile, overwrite: true);

                var hash = await Sha256Hasher.HashFileAsync(destFile, cancellationToken);
                var size = new FileInfo(destFile).Length;
                var archiveRelativePath = $"{archiveRelativePrefix}/{relativeToSourceRoot.Replace('\\', '/')}";
                copiedFiles.Add(new CopiedFileRecord(archiveRelativePath, destFile, size, hash));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
            {
                logger.LogWarning(ex, "Skipped file that could not be copied: {Path}", PathRedactor.Redact(sourceFile));
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Skipped (could not copy): {PathRedactor.Redact(sourceFile)} — {ex.Message}");
            }
        }
    }

    private async Task CopyInstallerFilesAsync(
        IReadOnlyList<string> installerFilePaths,
        string archiveRoot,
        List<CopiedFileRecord> copiedFiles,
        List<string> buildLog,
        IProgress<ArchiveBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        var installersDir = Path.Combine(archiveRoot, "Installers");
        Directory.CreateDirectory(installersDir);
        var processed = 0;

        foreach (var sourceFile in installerFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            if (!File.Exists(sourceFile))
            {
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Skipped installer file (no longer exists): {PathRedactor.Redact(sourceFile)}");
                continue;
            }

            var fileName = Path.GetFileName(sourceFile);
            progress?.Report(new ArchiveBuildProgress
            {
                Stage = ArchiveBuildStage.CopyingFiles,
                CurrentItem = fileName,
                FilesProcessed = processed,
                TotalFiles = installerFilePaths.Count,
            });

            try
            {
                var destFile = Path.Combine(installersDir, fileName);
                File.Copy(sourceFile, destFile, overwrite: true);

                var hash = await Sha256Hasher.HashFileAsync(destFile, cancellationToken);
                var size = new FileInfo(destFile).Length;
                copiedFiles.Add(new CopiedFileRecord($"Installers/{fileName}", destFile, size, hash));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
            {
                logger.LogWarning(ex, "Skipped installer file that could not be copied: {Path}", PathRedactor.Redact(sourceFile));
                buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Skipped (could not copy): {PathRedactor.Redact(sourceFile)} — {ex.Message}");
            }
        }
    }

    private async Task CopyManifestAsync(string manifestPath, string archiveRoot, List<CopiedFileRecord> copiedFiles, List<string> buildLog, CancellationToken cancellationToken)
    {
        try
        {
            var platformDir = Path.Combine(archiveRoot, "Platform");
            Directory.CreateDirectory(platformDir);
            var destFile = Path.Combine(platformDir, Path.GetFileName(manifestPath));
            File.Copy(manifestPath, destFile, overwrite: true);

            var hash = await Sha256Hasher.HashFileAsync(destFile, cancellationToken);
            var size = new FileInfo(destFile).Length;
            copiedFiles.Add(new CopiedFileRecord($"Platform/{Path.GetFileName(manifestPath)}", destFile, size, hash));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not copy platform manifest {Path}", PathRedactor.Redact(manifestPath));
            buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Warning: could not copy platform manifest — {ex.Message}");
        }
    }
}
