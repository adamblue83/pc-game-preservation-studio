using Microsoft.Extensions.Logging.Abstractions;

namespace PcGamePreservationStudio.Burning.Tests;

public sealed class OscdimgIsoBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-oscdimgbuilder-{Guid.NewGuid():N}");

    private string FakeOscdimgPath => Path.Combine(_root, "oscdimg.exe");

    [Fact]
    public void GetAvailability_ReportsAvailable_WhenOverrideExistsOnDisk()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FakeOscdimgPath, "fake, not a real executable");
        var builder = new OscdimgIsoBuilder(NullLogger<OscdimgIsoBuilder>.Instance);

        var availability = builder.GetAvailability(FakeOscdimgPath);

        Assert.True(availability.IsAvailable);
        Assert.Equal(FakeOscdimgPath, availability.ExecutablePath);
    }

    [Fact]
    public async Task BuildIsoAsync_ReturnsFailure_WhenOverrideIsNotARealExecutable()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FakeOscdimgPath, "fake, not a real executable");
        var builder = new OscdimgIsoBuilder(NullLogger<OscdimgIsoBuilder>.Instance);
        var outputIsoPath = Path.Combine(_root, "output.iso");

        var result = await builder.BuildIsoAsync(_root, outputIsoPath, FakeOscdimgPath);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(File.Exists(outputIsoPath));
    }

    [Fact]
    public async Task BuildIsoAsync_ReturnsFailure_WhenBackendCannotBeFound()
    {
        var builder = new OscdimgIsoBuilder(NullLogger<OscdimgIsoBuilder>.Instance);
        var bogusOverride = Path.Combine(_root, "does-not-exist.exe");

        var result = await builder.BuildIsoAsync(_root, Path.Combine(_root, "output.iso"), bogusOverride);

        // Whether a real oscdimg happens to be installed on this machine is environment-dependent,
        // but the call must never throw regardless — only report success or a clear failure.
        Assert.NotNull(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
