using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveBuilderIsoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-archivebuilder-iso-{Guid.NewGuid():N}");

    private string SourceDir => Path.Combine(_root, "Source", "MyGame");

    private string DestinationDir => Path.Combine(_root, "Destination");

    private async Task<GameLibraryEntry> CreateSourceGameAsync()
    {
        Directory.CreateDirectory(SourceDir);
        await File.WriteAllTextAsync(Path.Combine(SourceDir, "game.exe"), "fake executable contents");
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
    public async Task BuildAsync_WithIsoOnly_AndSuccessfulBackend_ProducesIsoAndRemovesStagedFolder()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: true));

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir, MediaType = MediaType.IsoOnly });

        Assert.True(result.Success);
        Assert.NotNull(result.IsoPath);
        Assert.Null(result.IsoBuildWarning);
        Assert.True(File.Exists(result.IsoPath));
        Assert.False(Directory.Exists(result.ArchiveFolderPath));
    }

    [Fact]
    public async Task BuildAsync_WithIsoOnly_AndFailedBackend_KeepsStagedFolderAndReportsWarning()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new FakeIsoBuilder(succeeds: false, errorMessage: "oscdimg not found"));

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir, MediaType = MediaType.IsoOnly });

        Assert.True(result.Success);
        Assert.Null(result.IsoPath);
        Assert.Equal("oscdimg not found", result.IsoBuildWarning);
        Assert.True(Directory.Exists(result.ArchiveFolderPath));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Game", "game.exe")));
    }

    [Fact]
    public async Task BuildAsync_WithIsoOnly_AndSuccessfulBackend_IncludesBuildLogInsideStagedFolderBeforeDeletion()
    {
        var game = await CreateSourceGameAsync();
        var isoBuilder = new RecordingIsoBuilder();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), isoBuilder);

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir, MediaType = MediaType.IsoOnly });

        Assert.True(result.Success);
        Assert.True(isoBuilder.SourceFolderHadBuildLogAtBuildTime);
    }

    /// <summary>Confirms build_log.txt already exists in the staged folder at the moment BuildIsoAsync is invoked.</summary>
    private sealed class RecordingIsoBuilder : PcGamePreservationStudio.Core.Abstractions.IIsoBuilder
    {
        public bool SourceFolderHadBuildLogAtBuildTime { get; private set; }

        public PcGamePreservationStudio.Core.Models.IsoBackendAvailability GetAvailability(string? oscdimgPathOverride = null) =>
            new() { IsAvailable = true, StatusMessage = "Available" };

        public async Task<PcGamePreservationStudio.Core.Models.IsoBuildResult> BuildIsoAsync(string sourceFolder, string outputIsoPath, string? oscdimgPathOverride = null, CancellationToken cancellationToken = default)
        {
            SourceFolderHadBuildLogAtBuildTime = File.Exists(Path.Combine(sourceFolder, "Metadata", "build_log.txt"));
            await File.WriteAllTextAsync(outputIsoPath, "fake iso contents", cancellationToken);
            return new PcGamePreservationStudio.Core.Models.IsoBuildResult { Success = true, IsoPath = outputIsoPath, IsoSizeBytes = new FileInfo(outputIsoPath).Length };
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
