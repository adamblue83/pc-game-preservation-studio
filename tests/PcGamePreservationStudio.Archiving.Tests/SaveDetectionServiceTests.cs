using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class SaveDetectionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-savedetect-{Guid.NewGuid():N}");

    [Fact]
    public async Task DetectSaveLocationsAsync_FindsHighConfidenceMatch_WhenInstallDirNameMatchesFolderInRoot()
    {
        var savesRoot = Path.Combine(_root, "Documents");
        Directory.CreateDirectory(Path.Combine(savesRoot, "Dota 2 Beta"));

        var service = new SaveDetectionService(
            NullLogger<SaveDetectionService>.Instance,
            [new SaveSearchRoot(savesRoot, "Documents", SaveLocationConfidence.High)]);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2 Beta",
        };

        var candidates = await service.DetectSaveLocationsAsync(game);

        var match = Assert.Single(candidates);
        Assert.Equal(Path.Combine(savesRoot, "Dota 2 Beta"), match.FullPath);
        Assert.Equal(SaveLocationConfidence.High, match.Confidence);
    }

    [Fact]
    public async Task DetectSaveLocationsAsync_ReturnsNoCandidates_WhenNoMatchingFolderExists()
    {
        var savesRoot = Path.Combine(_root, "Documents");
        Directory.CreateDirectory(savesRoot);
        Directory.CreateDirectory(Path.Combine(savesRoot, "Some Unrelated App"));

        var service = new SaveDetectionService(
            NullLogger<SaveDetectionService>.Instance,
            [new SaveSearchRoot(savesRoot, "Documents", SaveLocationConfidence.High)]);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2 Beta",
        };

        var candidates = await service.DetectSaveLocationsAsync(game);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task DetectSaveLocationsAsync_DemotesConfidence_ForTitleOnlyMatch()
    {
        var savesRoot = Path.Combine(_root, "Documents");
        // Folder matches the game's *title*, not its install directory name.
        Directory.CreateDirectory(Path.Combine(savesRoot, "Dota 2"));

        var service = new SaveDetectionService(
            NullLogger<SaveDetectionService>.Instance,
            [new SaveSearchRoot(savesRoot, "Documents", SaveLocationConfidence.High)]);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2 Beta",
        };

        var candidates = await service.DetectSaveLocationsAsync(game);

        var match = Assert.Single(candidates);
        Assert.Equal(SaveLocationConfidence.Medium, match.Confidence);
    }

    [Fact]
    public async Task DetectSaveLocationsAsync_DoesNotReturnDuplicates_WhenTitleAndInstallDirNameMatchTheSameFolder()
    {
        var savesRoot = Path.Combine(_root, "Documents");
        Directory.CreateDirectory(Path.Combine(savesRoot, "Dota 2"));

        var service = new SaveDetectionService(
            NullLogger<SaveDetectionService>.Instance,
            [new SaveSearchRoot(savesRoot, "Documents", SaveLocationConfidence.High)]);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2",
        };

        var candidates = await service.DetectSaveLocationsAsync(game);

        Assert.Single(candidates);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
