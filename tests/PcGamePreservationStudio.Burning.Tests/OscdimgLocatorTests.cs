namespace PcGamePreservationStudio.Burning.Tests;

public sealed class OscdimgLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-oscdimg-{Guid.NewGuid():N}");

    [Fact]
    public void Locate_ReturnsOverride_WhenItExistsOnDisk()
    {
        Directory.CreateDirectory(_root);
        var fakeExe = Path.Combine(_root, "oscdimg.exe");
        File.WriteAllText(fakeExe, "fake");

        var result = OscdimgLocator.Locate(fakeExe);

        Assert.Equal(fakeExe, result);
    }

    [Fact]
    public void Locate_IgnoresOverride_WhenItDoesNotExistOnDisk()
    {
        var bogusOverride = Path.Combine(_root, "does-not-exist.exe");

        var result = OscdimgLocator.Locate(bogusOverride);

        Assert.NotEqual(bogusOverride, result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
