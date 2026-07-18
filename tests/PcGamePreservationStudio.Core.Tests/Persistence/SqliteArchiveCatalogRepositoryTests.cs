using Microsoft.Data.Sqlite;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Persistence;

namespace PcGamePreservationStudio.Core.Tests.Persistence;

public sealed class SqliteArchiveCatalogRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pgps-catalog-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenCatalogIsNew()
    {
        var repository = new SqliteArchiveCatalogRepository(_dbPath);

        var entries = await repository.GetAllAsync();

        Assert.Empty(entries);
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_ReturnsTheAddedEntry()
    {
        var repository = new SqliteArchiveCatalogRepository(_dbPath);
        var entry = new ArchiveCatalogEntry
        {
            Id = Guid.NewGuid(),
            GameTitle = "Dota 2",
            Platform = GamePlatform.Steam,
            CreatedUtc = DateTimeOffset.UtcNow,
            DestinationType = ArchiveDestinationType.Folder,
            Status = ArchiveStatus.Draft,
        };

        await repository.AddAsync(entry);
        var entries = await repository.GetAllAsync();

        var stored = Assert.Single(entries);
        Assert.Equal(entry.Id, stored.Id);
        Assert.Equal(entry.GameTitle, stored.GameTitle);
        Assert.Equal(entry.Platform, stored.Platform);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools native connections by default, which keeps a file handle
        // open after the SqliteConnection is disposed unless the pool is explicitly cleared.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
