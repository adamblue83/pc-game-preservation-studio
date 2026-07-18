using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveDiscSplitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-discsplit-{Guid.NewGuid():N}");

    private string SourceDir => Path.Combine(_root, "Source", "MyGame");

    private string DestinationDir => Path.Combine(_root, "Destination");

    private async Task<GameLibraryEntry> CreateSourceGameAsync(int fileCount, int bytesPerFile)
    {
        Directory.CreateDirectory(SourceDir);
        for (var i = 0; i < fileCount; i++)
        {
            await File.WriteAllBytesAsync(Path.Combine(SourceDir, $"file{i}.bin"), new byte[bytesPerFile]);
        }

        Directory.CreateDirectory(DestinationDir);

        return new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.LocalFolder,
            Title = "My Game",
            InstallPath = SourceDir,
        };
    }

    [Fact]
    public async Task BuildAsync_SplitsIntoMultipleDiscFolders_WhenArchiveExceedsOneDiscsCapacity()
    {
        // 4 files x 400KB = 1.6MB total, against a 1MB "Custom" disc with no safety margin: forces a split.
        var game = await CreateSourceGameAsync(fileCount: 4, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Custom,
            CustomCapacityBytes = 1_000_000,
            SafetyMarginOverrideBytes = 0,
        });

        Assert.True(result.Success);
        Assert.True(result.DiscCount > 1);
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_02")));

        // The flat Game\ folder should no longer exist — its files were moved into the DISC_NN\ folders.
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "Game")));

        // Every disc has its own scoped checksums file.
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01", "Checksums", "SHA256SUMS.txt")));

        // The overall archive keeps a disc map, and no longer has a (now-stale) flat Checksums\ folder.
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Metadata", "disc_map.json")));
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "Checksums")));
    }

    [Fact]
    public async Task BuildAsync_VerifiesSuccessfully_AfterSplittingAcrossDiscs()
    {
        var game = await CreateSourceGameAsync(fileCount: 4, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Custom,
            CustomCapacityBytes = 1_000_000,
            SafetyMarginOverrideBytes = 0,
        });

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var verification = await verificationService.VerifyAsync(result.ArchiveFolderPath);

        Assert.Equal(VerificationOutcome.Verified, verification.Outcome);
    }

    [Fact]
    public async Task BuildAsync_StaysAsOneFolder_WhenMediaTypeIsFolderOnly()
    {
        var game = await CreateSourceGameAsync(fileCount: 2, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.FolderOnly,
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.DiscCount);
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Checksums", "SHA256SUMS.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
