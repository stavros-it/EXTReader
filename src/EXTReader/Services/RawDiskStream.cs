using System.IO;
using System.Runtime.InteropServices;
using EXTReader.Interop;
using Microsoft.Win32.SafeHandles;

namespace EXTReader.Services;

internal sealed class RawDiskStream : Stream
{
    private readonly SafeFileHandle _handle;
    private readonly uint _sectorSize;
    private long _position;
    private byte[]? _sectorBuf;
    private long _sectorBufStart = -1;

    public RawDiskStream(SafeFileHandle handle, uint sectorSize = 512)
    {
        _handle = handle;
        _sectorSize = sectorSize > 0 ? sectorSize : 512;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Span<byte> span = buffer.AsSpan(offset, count);
        return Read(span);
    }

    public override int Read(Span<byte> buffer)
    {
        if (_handle.IsInvalid) return 0;
        if (buffer.IsEmpty) return 0;

        _sectorBuf ??= new byte[_sectorSize];
        int totalRead = 0;

        while (!buffer.IsEmpty)
        {
            long sectorStart = (_position / _sectorSize) * _sectorSize;
            int offsetInSector = (int)(_position - sectorStart);

            if (_sectorBufStart != sectorStart)
            {
                if (!NativeKernel32.SetFilePointerEx(_handle, sectorStart, out _, 0))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException($"SetFilePointerEx failed with error {err} at pos {_position}", err);
                }

                uint toRead = _sectorSize;
                if (!NativeKernel32.ReadFile(_handle, _sectorBuf, toRead, out uint read, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 38) break;
                    throw new IOException($"ReadFile failed with error {err} at sector offset {sectorStart}", err);
                }

                if (read == 0) break;
                _sectorBufStart = sectorStart;
            }

            int available = (int)_sectorSize - offsetInSector;
            int toCopy = Math.Min(available, buffer.Length);
            _sectorBuf.AsSpan(offsetInSector, toCopy).CopyTo(buffer);
            buffer = buffer[toCopy..];
            _position += toCopy;
            totalRead += toCopy;
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => throw new NotSupportedException(),
            _ => _position
        };
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _handle.Dispose();
        }
        base.Dispose(disposing);
    }
}
