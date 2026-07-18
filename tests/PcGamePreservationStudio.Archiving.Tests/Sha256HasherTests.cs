using PcGamePreservationStudio.Archiving;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class Sha256HasherTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"pgps-hash-{Guid.NewGuid():N}.txt");

    [Fact]
    public async Task HashFileAsync_ProducesTheKnownSha256ForItsContent()
    {
        await File.WriteAllTextAsync(_filePath, "abc");

        var hash = await Sha256Hasher.HashFileAsync(_filePath);

        // Well-known SHA-256 of the ASCII string "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public async Task HashFileAsync_ProducesDifferentHashes_ForDifferentContent()
    {
        await File.WriteAllTextAsync(_filePath, "abc");
        var first = await Sha256Hasher.HashFileAsync(_filePath);

        await File.WriteAllTextAsync(_filePath, "abcd");
        var second = await Sha256Hasher.HashFileAsync(_filePath);

        Assert.NotEqual(first, second);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
