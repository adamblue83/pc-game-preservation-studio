using System.Runtime.InteropServices;

namespace PcGamePreservationStudio.Burning.Tests;

public sealed class ComStreamWrapperTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"pgps-comstream-{Guid.NewGuid():N}.bin");
    private readonly byte[] _content = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    public ComStreamWrapperTests()
    {
        File.WriteAllBytes(_filePath, _content);
    }

    private ComStreamWrapper CreateWrapper() =>
        new(new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

    [Fact]
    public void Stat_ReportsFileSize()
    {
        using var wrapper = CreateWrapper();

        wrapper.Stat(out var statstg, 0);

        Assert.Equal(_content.Length, statstg.cbSize);
    }

    [Fact]
    public void Read_ReturnsFileBytesInOrder()
    {
        using var wrapper = CreateWrapper();
        var buffer = new byte[_content.Length];
        var bytesReadPtr = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            wrapper.Read(buffer, buffer.Length, bytesReadPtr);

            Assert.Equal(_content.Length, Marshal.ReadInt32(bytesReadPtr));
            Assert.Equal(_content, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(bytesReadPtr);
        }
    }

    [Fact]
    public void Seek_RepositionsSubsequentReads()
    {
        using var wrapper = CreateWrapper();
        var newPositionPtr = Marshal.AllocHGlobal(sizeof(long));

        try
        {
            wrapper.Seek(5, (int)SeekOrigin.Begin, newPositionPtr);
            Assert.Equal(5, Marshal.ReadInt64(newPositionPtr));

            var buffer = new byte[2];
            wrapper.Read(buffer, buffer.Length, IntPtr.Zero);

            Assert.Equal(_content[5], buffer[0]);
            Assert.Equal(_content[6], buffer[1]);
        }
        finally
        {
            Marshal.FreeHGlobal(newPositionPtr);
        }
    }

    [Fact]
    public void Write_ThrowsNotSupported()
    {
        using var wrapper = CreateWrapper();

        Assert.Throws<NotSupportedException>(() => wrapper.Write([1, 2, 3], 3, IntPtr.Zero));
    }

    [Fact]
    public void SetSize_ThrowsNotSupported()
    {
        using var wrapper = CreateWrapper();

        Assert.Throws<NotSupportedException>(() => wrapper.SetSize(100));
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
