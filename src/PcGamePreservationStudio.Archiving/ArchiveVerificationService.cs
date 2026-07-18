using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving;

/// <summary>
/// Verifies a folder-based archive by re-reading and re-hashing every file against
/// Checksums\SHA256SUMS.txt — or, for a multi-disc archive (no root Checksums\, since it was split
/// into DISC_NN\ folders each with their own), against every DISC_NN\Checksums\SHA256SUMS.txt found.
/// Never reports <see cref="VerificationOutcome.Verified"/> without doing this read-back — a
/// successful build/copy is not treated as proof of integrity.
/// </summary>
public sealed class ArchiveVerificationService(ILogger<ArchiveVerificationService> logger) : IArchiveVerificationService
{
    private static readonly string[] ContentFolders = ["Game", "Platform", "Saves", "Installers"];

    public async Task<VerificationResult> VerifyAsync(string archiveFolderPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var rootChecksumsFile = Path.Combine(archiveFolderPath, "Checksums", "SHA256SUMS.txt");

        var aggregate = new ScopeResult();
        var scopesVerified = 0;

        if (File.Exists(rootChecksumsFile))
        {
            aggregate.Merge(await VerifyScopeAsync(archiveFolderPath, rootChecksumsFile, relativePrefix: null, cancellationToken));
            scopesVerified++;
        }
        else if (Directory.Exists(archiveFolderPath))
        {
            foreach (var discDir in Directory.EnumerateDirectories(archiveFolderPath, "DISC_*").OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var discName = Path.GetFileName(discDir);
                var discChecksumsFile = Path.Combine(discDir, "Checksums", "SHA256SUMS.txt");

                if (!File.Exists(discChecksumsFile))
                {
                    aggregate.Missing.Add($"{discName}/Checksums/SHA256SUMS.txt");
                    continue;
                }

                aggregate.Merge(await VerifyScopeAsync(discDir, discChecksumsFile, discName, cancellationToken));
                scopesVerified++;
            }
        }

        stopwatch.Stop();

        if (scopesVerified == 0)
        {
            return new VerificationResult { Outcome = VerificationOutcome.Incomplete, Duration = stopwatch.Elapsed };
        }

        return new VerificationResult
        {
            Outcome = DetermineOutcome(aggregate),
            Duration = stopwatch.Elapsed,
            MissingFiles = aggregate.Missing,
            ModifiedFiles = aggregate.Modified,
            UnreadableFiles = aggregate.Unreadable,
            UnexpectedFiles = aggregate.Unexpected,
        };
    }

    private async Task<ScopeResult> VerifyScopeAsync(string baseFolderPath, string checksumsFile, string? relativePrefix, CancellationToken cancellationToken)
    {
        var result = new ScopeResult();
        var expected = await ReadExpectedHashesAsync(checksumsFile, cancellationToken);

        foreach (var (relativePath, expectedHash) in expected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(baseFolderPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var displayPath = Prefix(relativePrefix, relativePath);

            if (!File.Exists(fullPath))
            {
                result.Missing.Add(displayPath);
                continue;
            }

            try
            {
                var actualHash = await Sha256Hasher.HashFileAsync(fullPath, cancellationToken);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.Modified.Add(displayPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {Path} during verification", displayPath);
                result.Unreadable.Add(displayPath);
            }
        }

        foreach (var relative in FindUnexpectedFiles(baseFolderPath, expected.Keys))
        {
            result.Unexpected.Add(Prefix(relativePrefix, relative));
        }

        return result;
    }

    private static string Prefix(string? relativePrefix, string relativePath) =>
        relativePrefix is null ? relativePath : $"{relativePrefix}/{relativePath}";

    private static VerificationOutcome DetermineOutcome(ScopeResult result)
    {
        if (result.Missing.Count > 0 || result.Modified.Count > 0 || result.Unreadable.Count > 0)
        {
            return VerificationOutcome.Failed;
        }

        return result.Unexpected.Count > 0 ? VerificationOutcome.VerifiedWithWarnings : VerificationOutcome.Verified;
    }

    private static async Task<Dictionary<string, string>> ReadExpectedHashesAsync(string checksumsFile, CancellationToken cancellationToken)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(checksumsFile, cancellationToken))
        {
            var separatorIndex = line.IndexOf("  ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var hash = line[..separatorIndex];
            var relativePath = line[(separatorIndex + 2)..];
            expected[relativePath] = hash;
        }

        return expected;
    }

    private static List<string> FindUnexpectedFiles(string baseFolderPath, IEnumerable<string> expectedRelativePaths)
    {
        var expectedSet = new HashSet<string>(expectedRelativePaths, StringComparer.Ordinal);
        var unexpected = new List<string>();

        foreach (var folder in ContentFolders)
        {
            var folderPath = Path.Combine(baseFolderPath, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                var relative = $"{folder}/{Path.GetRelativePath(folderPath, file).Replace('\\', '/')}";
                if (!expectedSet.Contains(relative))
                {
                    unexpected.Add(relative);
                }
            }
        }

        return unexpected;
    }

    private sealed class ScopeResult
    {
        public List<string> Missing { get; } = [];
        public List<string> Modified { get; } = [];
        public List<string> Unreadable { get; } = [];
        public List<string> Unexpected { get; } = [];

        public void Merge(ScopeResult other)
        {
            Missing.AddRange(other.Missing);
            Modified.AddRange(other.Modified);
            Unreadable.AddRange(other.Unreadable);
            Unexpected.AddRange(other.Unexpected);
        }
    }
}
