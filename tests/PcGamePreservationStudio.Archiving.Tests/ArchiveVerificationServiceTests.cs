using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveVerificationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-verify-{Guid.NewGuid():N}");

    private async Task<string> BuildSampleArchiveAsync()
    {
        var sourceDir = Path.Combine(_root, "Source", "MyGame");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "game.exe"), "fake executable contents");

        var destinationDir = Path.Combine(_root, "Destination");
        Directory.CreateDirectory(destinationDir);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.LocalFolder,
            Title = "My Game",
            InstallPath = sourceDir,
        };

        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());
        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = destinationDir });
        return result.ArchiveFolderPath;
    }

    [Fact]
    public async Task VerifyAsync_ReportsVerified_ForAnUntouchedArchive()
    {
        var archivePath = await BuildSampleArchiveAsync();
        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);

        var result = await verificationService.VerifyAsync(archivePath);

        Assert.Equal(VerificationOutcome.Verified, result.Outcome);
        Assert.Empty(result.MissingFiles);
        Assert.Empty(result.ModifiedFiles);
    }

    [Fact]
    public async Task VerifyAsync_ReportsFailed_WhenAFileIsModifiedAfterBuilding()
    {
        var archivePath = await BuildSampleArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(archivePath, "Game", "game.exe"), "corrupted contents");

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var result = await verificationService.VerifyAsync(archivePath);

        Assert.Equal(VerificationOutcome.Failed, result.Outcome);
        Assert.Contains("Game/game.exe", result.ModifiedFiles);
    }

    [Fact]
    public async Task VerifyAsync_ReportsFailed_WhenAFileIsDeletedAfterBuilding()
    {
        var archivePath = await BuildSampleArchiveAsync();
        File.Delete(Path.Combine(archivePath, "Game", "game.exe"));

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var result = await verificationService.VerifyAsync(archivePath);

        Assert.Equal(VerificationOutcome.Failed, result.Outcome);
        Assert.Contains("Game/game.exe", result.MissingFiles);
    }

    [Fact]
    public async Task VerifyAsync_ReportsVerifiedWithWarnings_WhenAnUnexpectedExtraFileIsPresent()
    {
        var archivePath = await BuildSampleArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(archivePath, "Game", "unexpected.txt"), "not part of the archive");

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var result = await verificationService.VerifyAsync(archivePath);

        Assert.Equal(VerificationOutcome.VerifiedWithWarnings, result.Outcome);
        Assert.Contains("Game/unexpected.txt", result.UnexpectedFiles);
    }

    [Fact]
    public async Task VerifyAsync_ReportsIncomplete_WhenChecksumsFileIsMissing()
    {
        var archivePath = await BuildSampleArchiveAsync();
        File.Delete(Path.Combine(archivePath, "Checksums", "SHA256SUMS.txt"));

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var result = await verificationService.VerifyAsync(archivePath);

        Assert.Equal(VerificationOutcome.Incomplete, result.Outcome);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
