using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

/// <summary>
/// Covers converting each DISC_NN\ folder of a physical-optical-disc-sized archive into its own
/// DISC_NN.iso, so a multi-disc build is ready to hand to Burn Disc without a manual extra step.
/// </summary>
public sealed class ArchiveBuilderMultiDiscIsoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-multidisciso-{Guid.NewGuid():N}");

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
    public async Task BuildAsync_ConvertsEachDiscFolderToIso_WhenMediaTypeIsAPhysicalOpticalDisc()
    {
        // 2 files x 400KB = 800KB total; CD-R (700MB advertised) is comfortably one disc here,
        // so this exercises the single-disc case of the new per-disc ISO conversion.
        var game = await CreateSourceGameAsync(fileCount: 2, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: true));

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Cd700,
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.DiscCount);
        Assert.Single(result.DiscIsoPaths);
        Assert.True(File.Exists(result.DiscIsoPaths[0]));
        Assert.EndsWith("DISC_01.iso", result.DiscIsoPaths[0]);
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.Null(result.IsoBuildWarning);
    }

    [Fact]
    public async Task BuildAsync_ConvertsEveryDiscFolderToIso_WhenArchiveSpansMultipleDiscs()
    {
        // Cd700's fixed 700MB advertised capacity can't be overridden via CustomCapacityBytes (that
        // only applies to media types with no fixed size, like Custom), so a huge safety-margin
        // override is used instead to shrink the usable-per-disc capacity down to ~900KB — just
        // enough to fit 2 of these 400KB files per disc, forcing a 2-disc split cheaply.
        var game = await CreateSourceGameAsync(fileCount: 4, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: true));

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Cd700,
            SafetyMarginOverrideBytes = 685_100_000,
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.DiscCount);
        Assert.Equal(2, result.DiscIsoPaths.Count);
        Assert.All(result.DiscIsoPaths, path => Assert.True(File.Exists(path)));
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_02")));
        Assert.Null(result.IsoBuildWarning);
    }

    [Fact]
    public async Task BuildAsync_KeepsFolderAndReportsWarning_WhenADiscsIsoBuildFails()
    {
        var game = await CreateSourceGameAsync(fileCount: 4, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: false, errorMessage: "oscdimg not found"));

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Cd700,
            SafetyMarginOverrideBytes = 685_100_000,
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.DiscCount);
        Assert.Empty(result.DiscIsoPaths);
        Assert.NotNull(result.IsoBuildWarning);
        Assert.Contains("oscdimg not found", result.IsoBuildWarning);
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_02")));
    }

    [Fact]
    public async Task BuildAsync_DoesNotBuildIsos_WhenMediaTypeIsCustom()
    {
        // Custom/Usb/ExternalDrive represent capacity planning for non-optical destinations —
        // there's nothing to burn, so no ISO should be built even though the folders get split.
        var game = await CreateSourceGameAsync(fileCount: 4, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: true));

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.Custom,
            CustomCapacityBytes = 1_000_000,
            SafetyMarginOverrideBytes = 0,
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.DiscCount);
        Assert.Empty(result.DiscIsoPaths);
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_01")));
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "DISC_02")));
    }

    [Fact]
    public async Task BuildAsync_DoesNotBuildIsos_WhenMediaTypeIsFolderOnly()
    {
        var game = await CreateSourceGameAsync(fileCount: 2, bytesPerFile: 400_000);
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: true));

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            MediaType = MediaType.FolderOnly,
        });

        Assert.True(result.Success);
        Assert.Empty(result.DiscIsoPaths);
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
