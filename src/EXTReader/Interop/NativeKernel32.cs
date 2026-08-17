using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EXTReader.Interop;

internal static partial class NativeKernel32
{
    private const string Kernel32 = "kernel32.dll";

    internal const uint GENERIC_READ = 0x80000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint OPEN_EXISTING = 3;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x0080;

    internal const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
    internal const uint IOCTL_DISK_GET_LENGTH_DISK = 0x0007405C;
    internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    [LibraryImport(Kernel32, SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "CreateFileW")]
    internal static partial SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DiskGeometry
    {
        public long Cylinders;
        public int MediaType;
        public uint TracksPerCylinder;
        public uint SectorsPerTrack;
        public uint BytesPerSector;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StoragePropertyQuery
    {
        public uint PropertyId;
        public uint QueryType;
        public byte AdditionalParameters;
    }

    [LibraryImport(Kernel32, SetLastError = true, EntryPoint = "LoadLibraryA")]
    internal static partial IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

    [LibraryImport(Kernel32, SetLastError = true, EntryPoint = "GetProcAddress")]
    internal static partial IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadFile(
        SafeFileHandle hFile,
        Span<byte> lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);
}
