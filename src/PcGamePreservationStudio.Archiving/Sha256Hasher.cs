using System.Security.Cryptography;

namespace PcGamePreservationStudio.Archiving;

/// <summary>Computes SHA-256 hashes with streaming reads so large game files don't need to be loaded into memory.</summary>
public static class Sha256Hasher
{
    private const int BufferSize = 1024 * 1024;

    public static async Task<string> HashFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
