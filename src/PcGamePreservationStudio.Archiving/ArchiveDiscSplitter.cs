using System.Text.Json;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving;

public sealed record DiscSplitResult(int DiscCount, IReadOnlyList<string> FilesTooLargeForMedium);

/// <summary>
/// Reorganizes an already-built flat archive into DISC_01\, DISC_02\, ... folders for disc-based
/// media, per docs/ARCHIVE_FORMAT.md. Moves files rather than copying (they already exist on disk
/// from the flat build) and never splits a single file across discs.
/// </summary>
public static class ArchiveDiscSplitter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] FlatContentFolders = ["Game", "Platform", "Saves", "Installers"];

    public static async Task<DiscSplitResult> SplitAsync(
        string archiveRoot,
        ArchiveBuildRequest request,
        IReadOnlyList<CopiedFileRecord> copiedFiles,
        IDiscCapacityService discCapacityService,
        List<string> buildLog,
        CancellationToken cancellationToken)
    {
        var plannedFiles = copiedFiles.Select(f => new PlannedFile(f.RelativePath, f.SizeBytes)).ToList();
        var plan = discCapacityService.PlanMultiDisc(plannedFiles, request.MediaType, request.CustomCapacityBytes, request.SafetyMarginOverrideBytes);

        foreach (var oversized in plan.FilesExceedingSingleDiscCapacity)
        {
            buildLog.Add($"[{DateTimeOffset.UtcNow:O}] File too large for a single {request.MediaType} disc — left in place, not assigned to any disc: {oversized.RelativePath} ({oversized.SizeBytes} bytes)");
        }

        var recordsByRelativePath = copiedFiles.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);

        foreach (var disc in plan.Discs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteDiscAsync(archiveRoot, request, disc, plan.DiscCount, recordsByRelativePath, cancellationToken);
        }

        RemoveEmptiedFlatFolders(archiveRoot);
        RemoveStaleFlatChecksums(archiveRoot);
        await WriteDiscMapAsync(archiveRoot, request, plan, cancellationToken);

        buildLog.Add($"[{DateTimeOffset.UtcNow:O}] Split into {plan.DiscCount} disc(s) for {request.MediaType}");

        return new DiscSplitResult(Math.Max(plan.DiscCount, 1), plan.FilesExceedingSingleDiscCapacity.Select(f => f.RelativePath).ToList());
    }

    private static async Task WriteDiscAsync(
        string archiveRoot,
        ArchiveBuildRequest request,
        DiscAssignment disc,
        int discCount,
        Dictionary<string, CopiedFileRecord> recordsByRelativePath,
        CancellationToken cancellationToken)
    {
        var discDir = Path.Combine(archiveRoot, $"DISC_{disc.DiscNumber:00}");
        Directory.CreateDirectory(discDir);

        var checksumLines = new List<string>();
        foreach (var plannedFile in disc.Files)
        {
            if (!recordsByRelativePath.TryGetValue(plannedFile.RelativePath, out var record))
            {
                continue;
            }

            var destPath = Path.Combine(discDir, plannedFile.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Move(record.AbsolutePath, destPath, overwrite: true);
            checksumLines.Add($"{record.Sha256}  {plannedFile.RelativePath}");
        }

        var checksumsDir = Path.Combine(discDir, "Checksums");
        Directory.CreateDirectory(checksumsDir);
        await File.WriteAllLinesAsync(
            Path.Combine(checksumsDir, "SHA256SUMS.txt"),
            checksumLines.OrderBy(l => l, StringComparer.Ordinal),
            cancellationToken);

        var metadataDir = Path.Combine(discDir, "Metadata");
        Directory.CreateDirectory(metadataDir);
        var discManifest = new
        {
            gameTitle = request.Game.Title,
            discNumber = disc.DiscNumber,
            discCount,
            mediaType = request.MediaType.ToString(),
            fileCount = disc.Files.Count,
            totalBytes = disc.TotalBytes,
        };
        await File.WriteAllTextAsync(Path.Combine(metadataDir, "disc_manifest.json"), JsonSerializer.Serialize(discManifest, JsonOptions), cancellationToken);
    }

    private static void RemoveEmptiedFlatFolders(string archiveRoot)
    {
        foreach (var folder in FlatContentFolders)
        {
            var path = Path.Combine(archiveRoot, folder);
            if (Directory.Exists(path) && !Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any())
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    /// <summary>
    /// The flat, whole-archive Checksums\ written before splitting now references paths that were
    /// moved into DISC_NN\ folders (each of which has its own scoped Checksums\), so it's stale and
    /// must be removed rather than left around to mislead a later verification pass.
    /// </summary>
    private static void RemoveStaleFlatChecksums(string archiveRoot)
    {
        var checksumsDir = Path.Combine(archiveRoot, "Checksums");
        if (Directory.Exists(checksumsDir))
        {
            Directory.Delete(checksumsDir, recursive: true);
        }
    }

    private static async Task WriteDiscMapAsync(string archiveRoot, ArchiveBuildRequest request, MultiDiscPlan plan, CancellationToken cancellationToken)
    {
        var discMap = new
        {
            mediaType = request.MediaType.ToString(),
            discCount = plan.DiscCount,
            safetyMarginBytes = plan.SafetyMarginBytes,
            discs = plan.Discs.Select(d => new { d.DiscNumber, fileCount = d.Files.Count, totalBytes = d.TotalBytes }),
            filesTooLargeForMedium = plan.FilesExceedingSingleDiscCapacity.Select(f => f.RelativePath),
        };

        var metadataDir = Path.Combine(archiveRoot, "Metadata");
        Directory.CreateDirectory(metadataDir);
        await File.WriteAllTextAsync(Path.Combine(metadataDir, "disc_map.json"), JsonSerializer.Serialize(discMap, JsonOptions), cancellationToken);
    }
}
