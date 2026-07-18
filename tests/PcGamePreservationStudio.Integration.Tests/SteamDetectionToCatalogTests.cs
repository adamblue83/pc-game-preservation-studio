using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Persistence;
using PcGamePreservationStudio.Platforms.Steam;

namespace PcGamePreservationStudio.Integration.Tests;

/// <summary>End-to-end: detect a game from a simulated Steam install, then persist it into the archive catalog.</summary>
public sealed class SteamDetectionToCatalogTests : IDisposable
{
    private readonly string _steamRoot = Path.Combine(Path.GetTempPath(), $"pgps-integration-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public SteamDetectionToCatalogTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pgps-integration-{Guid.NewGuid():N}.db");
    }

    private sealed class StubSettingsService(AppSettings settings) : ISettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(AppSettings newSettings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task DetectedSteamGame_CanBeRecordedInTheArchiveCatalog()
    {
        Directory.CreateDirectory(Path.Combine(_steamRoot, "steamapps"));
        File.WriteAllText(Path.Combine(_steamRoot, "steamapps", "appmanifest_570.acf"), """
            "AppState"
            {
            	"appid"		"570"
            	"name"		"Dota 2"
            	"installdir"		"dota 2 beta"
            	"SizeOnDisk"		"36854775808"
            }
            """);

        var steamProvider = new SteamGamePlatformProvider(
            new StubSettingsService(new AppSettings { SteamInstallPathOverride = _steamRoot }),
            NullLogger<SteamGamePlatformProvider>.Instance);

        var detectedGames = await steamProvider.GetGamesAsync();
        var dota = Assert.Single(detectedGames);

        var catalogRepository = new SqliteArchiveCatalogRepository(_dbPath);
        await catalogRepository.AddAsync(new ArchiveCatalogEntry
        {
            Id = Guid.NewGuid(),
            GameTitle = dota.Title,
            Platform = dota.Platform,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        var catalogEntries = await catalogRepository.GetAllAsync();
        var stored = Assert.Single(catalogEntries);
        Assert.Equal("Dota 2", stored.GameTitle);
        Assert.Equal(GamePlatform.Steam, stored.Platform);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_steamRoot))
        {
            Directory.Delete(_steamRoot, recursive: true);
        }

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
