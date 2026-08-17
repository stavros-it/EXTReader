using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using EXTReader.Interop;
using EXTReader.Models;

namespace EXTReader.Services;

public sealed class DriveDiscoveryService
{
    private const int MaxDriveIndex = 16;

    public List<PhysicalDriveInfo> DiscoverDrives()
    {
        var drives = new List<PhysicalDriveInfo>();

        for (int i = 0; i < MaxDriveIndex; i++)
        {
            var devicePath = $@"\\.\PhysicalDrive{i}";
            var drive = TryOpenDrive(i, devicePath);
            if (drive != null)
                drives.Add(drive);
        }

        return drives;
    }

    private static PhysicalDriveInfo? TryOpenDrive(int index, string devicePath)
    {
        var handle = OpenReadonly(devicePath);
        if (handle.IsInvalid)
            return null;

        long diskSize = GetDiskSize(handle);
        uint sectorSize = GetSectorSize(handle);
        if (sectorSize == 0)
            sectorSize = 512;

        string model = GetModelName(handle) ?? $"Physical Drive {index}";

        using var stream = new RawDiskStream(handle, sectorSize);

        byte[] firstSector = new byte[sectorSize];
        int read = stream.Read(firstSector, 0, firstSector.Length);
        if (read < 512)
            return null;

        var partitions = PartitionParser.Parse(firstSector, stream, sectorSize);

        for (int p = 0; p < partitions.Count; p++)
        {
            var part = partitions[p];
            part.FileSystem = ExtDetector.Detect(stream, part.StartOffset);
        }

        var tableType = partitions.Count > 0 || IsMbrSignatureValid(firstSector)
            ? PartitionParser.DetectTableType(firstSector)
            : PartitionTableType.None;

        return new PhysicalDriveInfo
        {
            Index = index,
            DevicePath = devicePath,
            Size = diskSize,
            Model = model,
            SectorSize = sectorSize,
            PartitionTable = tableType,
            Partitions = partitions,
        };
    }

    private static SafeFileHandle OpenReadonly(string devicePath)
    {
        return NativeKernel32.CreateFileW(
            devicePath,
            NativeKernel32.GENERIC_READ,
            NativeKernel32.FILE_SHARE_READ | NativeKernel32.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeKernel32.OPEN_EXISTING,
            NativeKernel32.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);
    }

    private static long GetDiskSize(SafeFileHandle handle)
    {
        IntPtr buffer = Marshal.AllocHGlobal(8);
        try
        {
            if (!NativeKernel32.DeviceIoControl(
                handle,
                NativeKernel32.IOCTL_DISK_GET_LENGTH_DISK,
                IntPtr.Zero, 0,
                buffer, 8,
                out _, IntPtr.Zero))
            {
                return 0;
            }

            return Marshal.ReadInt64(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static uint GetSectorSize(SafeFileHandle handle)
    {
        IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeKernel32.DiskGeometry>());
        try
        {
            if (!NativeKernel32.DeviceIoControl(
                handle,
                NativeKernel32.IOCTL_DISK_GET_DRIVE_GEOMETRY,
                IntPtr.Zero, 0,
                buffer, (uint)Marshal.SizeOf<NativeKernel32.DiskGeometry>(),
                out _, IntPtr.Zero))
            {
                return 512;
            }

            var geo = Marshal.PtrToStructure<NativeKernel32.DiskGeometry>(buffer);
            return geo.BytesPerSector == 0 ? 512 : geo.BytesPerSector;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string? GetModelName(SafeFileHandle handle)
    {
        int querySize = Marshal.SizeOf<NativeKernel32.StoragePropertyQuery>();
        IntPtr queryPtr = Marshal.AllocHGlobal(querySize);
        IntPtr outBuffer = Marshal.AllocHGlobal(4096);
        try
        {
            var query = new NativeKernel32.StoragePropertyQuery
            {
                PropertyId = 1,
                QueryType = 0,
            };
            Marshal.StructureToPtr(query, queryPtr, false);

            if (!NativeKernel32.DeviceIoControl(
                handle,
                NativeKernel32.IOCTL_STORAGE_QUERY_PROPERTY,
                queryPtr, (uint)querySize,
                outBuffer, 4096,
                out _, IntPtr.Zero))
            {
                return null;
            }

            int productIdOffset = Marshal.ReadInt32(outBuffer, 16);
            int vendorIdOffset = Marshal.ReadInt32(outBuffer, 12);

            string? product = productIdOffset > 0
                ? Marshal.PtrToStringAnsi(outBuffer + productIdOffset)
                : null;

            string? vendor = vendorIdOffset > 0
                ? Marshal.PtrToStringAnsi(outBuffer + vendorIdOffset)
                : null;

            if (!string.IsNullOrWhiteSpace(product) && !string.IsNullOrWhiteSpace(vendor))
                return $"{vendor.Trim()} {product.Trim()}".Trim();

            return product?.Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(queryPtr);
            Marshal.FreeHGlobal(outBuffer);
        }
    }

    private static bool IsMbrSignatureValid(byte[] sector)
    {
        return sector.Length >= 512
            && sector[510] == 0x55
            && sector[511] == 0xAA;
    }
}
