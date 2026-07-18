using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Persistence;

namespace PcGamePreservationStudio.Core.Tests.Persistence;

public sealed class JsonLocalFolderSourceRepositoryTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"pgps-local-folders-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var repository = new JsonLocalFolderSourceRepository(_filePath);

        var sources = await repository.GetAllAsync();

        Assert.Empty(sources);
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_ReturnsTheAddedSource()
    {
        var repository = new JsonLocalFolderSourceRepository(_filePath);
        var source = new LocalFolderSource
        {
            Id = Guid.NewGuid(),
            Path = @"D:\Games\MyGame",
            DisplayName = "My Game",
            Kind = LocalFolderSourceKind.InstalledGame,
        };

        await repository.AddAsync(source);
        var sources = await repository.GetAllAsync();

        var stored = Assert.Single(sources);
        Assert.Equal(source, stored);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
