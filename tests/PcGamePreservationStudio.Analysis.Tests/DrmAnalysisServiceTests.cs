using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Analysis;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Analysis.Tests;

public sealed class DrmAnalysisServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-drmanalysis-{Guid.NewGuid():N}");

    [Fact]
    public async Task AnalyzeAsync_ReturnsPlatformClientLikelyRequired_WhenSteamworksDllPresent()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "steam_api64.dll"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.PlatformClientLikelyRequired, result.Label);
        Assert.Equal(DrmAnalysisConfidence.Medium, result.Confidence);
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsThirdPartyLauncherLikelyRequired_WhenLauncherMarkerPresent()
    {
        var subfolder = Path.Combine(_root, "Binaries");
        Directory.CreateDirectory(subfolder);
        File.WriteAllText(Path.Combine(subfolder, "EACore.dll"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.ThirdPartyLauncherLikelyRequired, result.Label);
    }

    [Fact]
    public async Task AnalyzeAsync_PrefersThirdPartyLauncherOverSteamworks_WhenBothPresent()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "steam_api64.dll"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "UbisoftGameLauncher.exe"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.ThirdPartyLauncherLikelyRequired, result.Label);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsLikelyDrmFree_WhenGogGameHasNoLauncherMarkers()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "game.exe"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Gog,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.LikelyDrmFree, result.Label);
        Assert.Equal(DrmAnalysisConfidence.Medium, result.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsUnknownLowConfidence_WhenLocalFolderHasNoMarkers()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "game.exe"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.LocalFolder,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.Unknown, result.Label);
        Assert.Equal(DrmAnalysisConfidence.Low, result.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsUnknownLowConfidence_WhenInstallFolderMissing()
    {
        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Some Game",
            InstallPath = Path.Combine(_root, "does-not-exist"),
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.Unknown, result.Label);
        Assert.Equal(DrmAnalysisConfidence.Low, result.Confidence);
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task AnalyzeAsync_MatchesMarkerNamesCaseInsensitively()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "STEAM_API64.DLL"), string.Empty);

        var service = new DrmAnalysisService(NullLogger<DrmAnalysisService>.Instance);
        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Steam,
            Title = "Some Game",
            InstallPath = _root,
        };

        var result = await service.AnalyzeAsync(game);

        Assert.Equal(DrmAnalysisLabel.PlatformClientLikelyRequired, result.Label);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
