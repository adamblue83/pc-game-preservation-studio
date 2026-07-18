using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveMetadataWriterTests
{
    [Fact]
    public void AssessPreservation_RatesGood_ForSteamGameWithManifest()
    {
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2",
            ManifestPath = @"C:\Steam\steamapps\appmanifest_570.acf",
        };

        var assessment = ArchiveMetadataWriter.AssessPreservation(game, hasSaves: true, hasInstallerFiles: false);

        Assert.Equal(PreservationRating.Good, assessment.Rating);
        Assert.Contains(assessment.Reasons, r => r.Contains("manifest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(assessment.Reasons, r => r.Contains("Save files included", StringComparison.Ordinal));
    }

    [Fact]
    public void AssessPreservation_RatesUnknown_ForLocalFolderSource()
    {
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.LocalFolder,
            Title = "My Homebrew Game",
            InstallPath = @"D:\Games\MyHomebrewGame",
        };

        var assessment = ArchiveMetadataWriter.AssessPreservation(game, hasSaves: false, hasInstallerFiles: false);

        Assert.Equal(PreservationRating.Unknown, assessment.Rating);
    }

    [Fact]
    public void AssessPreservation_RatesUnknown_ForSteamGameWithoutManifest()
    {
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Dota 2",
            InstallPath = @"C:\Steam\steamapps\common\Dota 2",
            ManifestPath = null,
        };

        var assessment = ArchiveMetadataWriter.AssessPreservation(game, hasSaves: false, hasInstallerFiles: false);

        Assert.Equal(PreservationRating.Unknown, assessment.Rating);
    }

    [Fact]
    public void AssessPreservation_RatesExcellent_ForGogOfflineInstallerArchive()
    {
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Gog,
            Title = "The Witcher 3",
            InstallPath = string.Empty,
        };

        var assessment = ArchiveMetadataWriter.AssessPreservation(game, hasSaves: false, hasInstallerFiles: true);

        Assert.Equal(PreservationRating.Excellent, assessment.Rating);
        Assert.Contains(assessment.Reasons, r => r.Contains("DRM-free", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssessPreservation_RatesGood_ForInstalledGogGame()
    {
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Gog,
            Title = "Gun Metal",
            InstallPath = @"C:\Program Files (x86)\GOG Galaxy\Games\Gun Metal",
            PlatformAppId = "1719266458",
        };

        var assessment = ArchiveMetadataWriter.AssessPreservation(game, hasSaves: false, hasInstallerFiles: false);

        Assert.Equal(PreservationRating.Good, assessment.Rating);
        Assert.Contains(assessment.Reasons, r => r.Contains("GOG Galaxy", StringComparison.Ordinal));
    }
}
