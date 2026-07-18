using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Persistence;

namespace PcGamePreservationStudio.Core.Tests.Persistence;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"pgps-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _filePath);

        var settings = await service.LoadAsync();

        Assert.False(settings.HasCompletedFirstRun);
        Assert.Null(settings.SteamInstallPathOverride);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSettings()
    {
        var service = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance, _filePath);
        var original = new AppSettings
        {
            SteamInstallPathOverride = @"D:\Games\Steam",
            DefaultArchiveOutputFolder = @"E:\Archives",
            HasCompletedFirstRun = true,
            SkipPlatformDetection = true,
        };

        await service.SaveAsync(original);
        var loaded = await service.LoadAsync();

        Assert.Equal(original, loaded);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
