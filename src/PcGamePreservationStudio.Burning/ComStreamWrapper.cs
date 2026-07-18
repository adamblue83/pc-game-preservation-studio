using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PcGamePreservationStudio.Burning;

/// <summary>
/// Read-only COM <see cref="IStream"/> wrapper over a <see cref="FileStream"/>, used to feed an
/// already-built ISO file to <c>MsftDiscFormat2Data.Write()</c>. This lets burning reuse Phase 6's
/// oscdimg-built ISO directly instead of mastering a disc image from a folder via IMAPI2FS.
/// </summary>
public sealed class ComStreamWrapper(FileStream fileStream) : IStream, IDisposable
{
    public void Read(byte[] pv, int cb, IntPtr pcbRead)
    {
        var bytesRead = fileStream.Read(pv, 0, cb);
        if (pcbRead != IntPtr.Zero)
        {
            Marshal.WriteInt32(pcbRead, bytesRead);
        }
    }

    public void Write(byte[] pv, int cb, IntPtr pcbWritten) =>
        throw new NotSupportedException("This stream is read-only — it only feeds an existing ISO to the burner.");

    public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
    {
        var newPosition = fileStream.Seek(dlibMove, (SeekOrigin)dwOrigin);
        if (plibNewPosition != IntPtr.Zero)
        {
            Marshal.WriteInt64(plibNewPosition, newPosition);
        }
    }

    public void SetSize(long libNewSize) =>
        throw new NotSupportedException("This stream is read-only — it only feeds an existing ISO to the burner.");

    public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten) =>
        throw new NotSupportedException("This stream is read-only — it only feeds an existing ISO to the burner.");

    public void Commit(int grfCommitFlags)
    {
        // Nothing to flush — this is a read-only view over an already-complete file.
    }

    public void Revert()
    {
    }

    public void LockRegion(long libOffset, long cb, int dwLockType)
    {
        // Region locking isn't needed for a read-only single-consumer stream.
    }

    public void UnlockRegion(long libOffset, long cb, int dwLockType)
    {
    }

    public void Stat(out STATSTG pstatstg, int grfStatFlag)
    {
        pstatstg = new STATSTG { type = 2 /* STGTY_STREAM */, cbSize = fileStream.Length };
    }

    public void Clone(out IStream ppstm) =>
        throw new NotSupportedException("This stream is read-only — it only feeds an existing ISO to the burner.");

    public void Dispose() => fileStream.Dispose();
}
