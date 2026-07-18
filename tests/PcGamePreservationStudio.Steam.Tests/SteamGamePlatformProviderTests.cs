using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Platforms.Steam;

namespace PcGamePreservationStudio.Steam.Tests;

public sealed class SteamGamePlatformProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-steam-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetGamesAsync_ReturnsEmpty_WhenOverridePointsAtAFolderWithNoSteamapps()
    {
        // An existing-but-empty override directory must win over registry/default-path fallback,
        // so this stays isolated from whatever Steam install (if any) is actually on this machine.
        Directory.CreateDirectory(_root);

        var provider = new SteamGamePlatformProvider(
            new StubSettingsService(new AppSettings { SteamInstallPathOverride = _root }),
            NullLogger<SteamGamePlatformProvider>.Instance);

        var games = await provider.GetGamesAsync();

        Assert.Empty(games);
    }

    [Fact]
    public async Task GetGamesAsync_FindsGamesAcrossMultipleLibrariesAndSkipsInvalidManifests()
    {
        var secondLibrary = Path.Combine(_root, "SecondLibrary");
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));
        Directory.CreateDirectory(Path.Combine(secondLibrary, "steamapps"));

        File.WriteAllText(Path.Combine(_root, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"{{_root.Replace(@"\", @"\\")}}"
            	}
            	"1"
            	{
            		"path"		"{{secondLibrary.Replace(@"\", @"\\")}}"
            	}
            }
            """);

        File.WriteAllText(Path.Combine(_root, "steamapps", "appmanifest_570.acf"), """
            "AppState"
            {
            	"appid"		"570"
            	"name"		"Dota 2"
            	"installdir"		"dota 2 beta"
            	"SizeOnDisk"		"36854775808"
            }
            """);

        File.WriteAllText(Path.Combine(secondLibrary, "steamapps", "appmanifest_730.acf"), """
            "AppState"
            {
            	"appid"		"730"
            	"name"		"Counter-Strike 2"
            	"installdir"		"Counter-Strike Global Offensive"
            	"SizeOnDisk"		"53687091200"
            }
            """);

        // Missing "name" — should be skipped rather than throwing.
        File.WriteAllText(Path.Combine(secondLibrary, "steamapps", "appmanifest_999.acf"), """
            "AppState"
            {
            	"appid"		"999"
            	"installdir"		"broken"
            }
            """);

        var provider = new SteamGamePlatformProvider(
            new StubSettingsService(new AppSettings { SteamInstallPathOverride = _root }),
            NullLogger<SteamGamePlatformProvider>.Instance);

        var games = await provider.GetGamesAsync();

        Assert.Equal(2, games.Count);

        var dota = Assert.Single(games, g => g.PlatformAppId == "570");
        Assert.Equal("Dota 2", dota.Title);
        Assert.Equal(GamePlatform.Steam, dota.Platform);
        Assert.Equal(36854775808, dota.SizeOnDiskBytes);
        Assert.Equal(Path.Combine(_root, "steamapps", "common", "dota 2 beta"), dota.InstallPath);

        var cs2 = Assert.Single(games, g => g.PlatformAppId == "730");
        Assert.Equal("Counter-Strike 2", cs2.Title);
        Assert.Equal(Path.Combine(secondLibrary, "steamapps", "common", "Counter-Strike Global Offensive"), cs2.InstallPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
