using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-archivebuilder-{Guid.NewGuid():N}");

    private string SourceDir => Path.Combine(_root, "Source", "MyGame");

    private string DestinationDir => Path.Combine(_root, "Destination");

    private async Task<GameLibraryEntry> CreateSourceGameAsync()
    {
        Directory.CreateDirectory(SourceDir);
        Directory.CreateDirectory(Path.Combine(SourceDir, "bin"));
        await File.WriteAllTextAsync(Path.Combine(SourceDir, "game.exe"), "fake executable contents");
        await File.WriteAllTextAsync(Path.Combine(SourceDir, "bin", "data.pak"), "fake game data");
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
    public async Task BuildAsync_CopiesAndHashesAllSourceFiles()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir });

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalFiles);
        Assert.True(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "Game")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Game", "game.exe")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Game", "bin", "data.pak")));
    }

    [Fact]
    public async Task BuildAsync_WritesChecksumsForEveryCopiedFile()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir });

        var sumsFile = Path.Combine(result.ArchiveFolderPath, "Checksums", "SHA256SUMS.txt");
        Assert.True(File.Exists(sumsFile));
        var lines = await File.ReadAllLinesAsync(sumsFile);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Matches("^[a-f0-9]{64}  Game/", line));
    }

    [Fact]
    public async Task BuildAsync_WritesExpectedMetadataFiles()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir });

        var metadataDir = Path.Combine(result.ArchiveFolderPath, "Metadata");
        Assert.True(File.Exists(Path.Combine(metadataDir, "game_info.json")));
        Assert.True(File.Exists(Path.Combine(metadataDir, "preservation_report.json")));
        Assert.True(File.Exists(Path.Combine(metadataDir, "archive_manifest.json")));
        Assert.True(File.Exists(Path.Combine(metadataDir, "source_files.json")));
        Assert.True(File.Exists(Path.Combine(metadataDir, "build_log.txt")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "README.txt")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Restore", "README.txt")));
    }

    [Fact]
    public async Task BuildAsync_IncludesSelectedSaveLocation()
    {
        var game = await CreateSourceGameAsync();
        var saveDir = Path.Combine(_root, "Saves", "MyGameSave");
        Directory.CreateDirectory(saveDir);
        await File.WriteAllTextAsync(Path.Combine(saveDir, "save1.dat"), "save data");

        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());
        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            IncludedSaveLocationPaths = [saveDir],
        });

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Saves", "MyGameSave", "save1.dat")));
    }

    [Fact]
    public async Task BuildAsync_ThrowsOperationCanceled_WhenCancelledUpFront()
    {
        var game = await CreateSourceGameAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            builder.BuildAsync(new ArchiveBuildRequest { Game = game, DestinationFolder = DestinationDir }, cancellationToken: cts.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
