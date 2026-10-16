using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CyberManager.Core.Engine;

/// <summary>Reads the disk driver's counters without depending on the PerfDisk/PDH registration.</summary>
internal sealed class NativeDiskPerformanceSource : IDiskPerformanceSource
{
    private const uint DiskPerformanceControl = 0x00070020;
    private const uint StorageDeviceNumberControl = 0x002D1080;
    private const uint VolumeExtentsControl = 0x00560000;
    private static readonly Guid DiskInterfaceClass = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
    private readonly Dictionary<uint, Device> _devices = new();
    private long _nextDiscovery;
    private uint _lastError;

    public DiskCounterSample Read()
    {
        var now = Stopwatch.GetTimestamp();
        if (now >= _nextDiscovery)
        {
            Discover();
            _nextDiscovery = now + Stopwatch.Frequency * 5;
        }
        var readings = new List<DiskCounterReading>();
        var warming = false;
        foreach (var (number, device) in _devices)
        {
            if (!ReadPerformance(device.Handle, DiskPerformanceControl, IntPtr.Zero, 0,
                    out var counters, (uint)Marshal.SizeOf<DiskPerformance>(), out var returned, IntPtr.Zero))
            {
                _lastError = (uint)Marshal.GetLastWin32Error();
                device.Previous = null;
                continue;
            }
            if (returned < Marshal.SizeOf<DiskPerformance>()) { _lastError = 13; continue; }
            var timestamp = Stopwatch.GetTimestamp();
            var reading = device.Previous is { } previous
                ? Calculate(number.ToString(CultureInfo.InvariantCulture), previous, counters,
                    Stopwatch.GetElapsedTime(device.Timestamp, timestamp).TotalSeconds, device.Volumes)
                : null;
            device.Previous = counters;
            device.Timestamp = timestamp;
            if (reading != null) readings.Add(reading);
            else warming = true;
        }
        if (readings.Count > 0)
            return new(DiskMetricsStatus.Ready, readings.OrderBy(d => uint.Parse(d.Id, CultureInfo.InvariantCulture)).ToArray());
        return warming ? new(DiskMetricsStatus.WarmingUp, [])
            : new(DiskMetricsStatus.Unavailable, [], _lastError == 0 ? 1167u : _lastError);
    }

    internal static DiskCounterReading? Calculate(string id, DiskPerformance previous, DiskPerformance current,
        double elapsedSeconds, string? volumes = null)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0 || elapsedSeconds > 3 ||
            current.BytesRead < previous.BytesRead || current.BytesWritten < previous.BytesWritten ||
            current.IdleTime < previous.IdleTime || current.ReadTime < previous.ReadTime || current.WriteTime < previous.WriteTime)
            return null;
        var readDelta = current.BytesRead - previous.BytesRead;
        var writeDelta = current.BytesWritten - previous.BytesWritten;
        // DWORD operation counters can roll over while the 64-bit byte/time counters keep increasing.
        var operations = (long)unchecked(current.ReadCount - previous.ReadCount) + unchecked(current.WriteCount - previous.WriteCount);
        var requestTime = (current.ReadTime - previous.ReadTime) + (current.WriteTime - previous.WriteTime);
        return new(id,
            Math.Clamp((current.IdleTime - previous.IdleTime) / (elapsedSeconds * 10_000_000) * 100, 0, 100),
            readDelta / elapsedSeconds, writeDelta / elapsedSeconds, operations / elapsedSeconds,
            operations > 0 ? requestTime / 10_000.0 / operations : null,
            current.QueueDepth, current.BytesRead, current.BytesWritten, volumes);
    }

    private void Discover()
    {
        var diskClass = DiskInterfaceClass;
        var infoSet = SetupDiGetClassDevsW(ref diskClass, null, IntPtr.Zero, 0x12);
        if (infoSet == new IntPtr(-1)) { _lastError = (uint)Marshal.GetLastWin32Error(); return; }
        var present = new HashSet<uint>();
        var complete = false;
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref diskClass, index, ref data))
                {
                    var error = (uint)Marshal.GetLastWin32Error();
                    if (error == 259) complete = true;
                    else _lastError = error;
                    break;
                }
                if (!SetupDiGetDeviceInterfaceDetailW(infoSet, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero) &&
                    Marshal.GetLastWin32Error() != 122)
                { _lastError = (uint)Marshal.GetLastWin32Error(); continue; }
                if (size < 6) continue;
                var buffer = Marshal.AllocHGlobal(checked((int)size));
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(infoSet, ref data, buffer, size, out _, IntPtr.Zero))
                    { _lastError = (uint)Marshal.GetLastWin32Error(); continue; }
                    var path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4));
                    if (path == null) continue;
                    var handle = OpenDevice(path);
                    if (handle.IsInvalid) { _lastError = (uint)Marshal.GetLastWin32Error(); handle.Dispose(); continue; }
                    if (!ReadDeviceNumber(handle, StorageDeviceNumberControl, IntPtr.Zero, 0,
                            out var storage, (uint)Marshal.SizeOf<StorageDeviceNumber>(), out _, IntPtr.Zero))
                    { _lastError = (uint)Marshal.GetLastWin32Error(); handle.Dispose(); continue; }
                    present.Add(storage.Number);
                    if (_devices.TryGetValue(storage.Number, out var existing) && existing.Path == path)
                        handle.Dispose();
                    else
                    {
                        existing?.Handle.Dispose();
                        _devices[storage.Number] = new(path, handle);
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally
        {
            if (!SetupDiDestroyDeviceInfoList(infoSet))
                Trace.TraceWarning("Could not release disk device enumeration: {0}.", Marshal.GetLastWin32Error());
        }
        if (complete)
        {
            foreach (var number in _devices.Keys.Where(n => !present.Contains(n)).ToArray())
            { _devices[number].Handle.Dispose(); _devices.Remove(number); }
        }
        var volumes = DiscoverVolumes();
        foreach (var (number, device) in _devices) device.Volumes = volumes.GetValueOrDefault(number, string.Empty);
    }

    private static Dictionary<uint, string> DiscoverVolumes()
    {
        var volumes = new Dictionary<uint, List<string>>();
        var drives = GetLogicalDrives();
        for (var index = 0; index < 26; index++)
        {
            if ((drives & (1u << index)) == 0) continue;
            var letter = $"{(char)('A' + index)}:";
            using var volume = OpenDevice($@"\\.\{letter}");
            if (volume.IsInvalid) continue;
            // A DWORD is followed by aligned, 24-byte DISK_EXTENT entries (including spanned volumes).
            for (var size = 1024; size <= 65536; size *= 2)
            {
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (!ReadVolumeExtents(volume, VolumeExtentsControl, IntPtr.Zero, 0, buffer, (uint)size, out var returned, IntPtr.Zero))
                    {
                        if (Marshal.GetLastWin32Error() == 234) continue;
                        break;
                    }
                    if (returned < 8) break;
                    var count = Marshal.ReadInt32(buffer);
                    for (var extent = 0; extent < count && 8 + (extent + 1) * 24 <= returned; extent++)
                    {
                        var number = unchecked((uint)Marshal.ReadInt32(buffer, 8 + extent * 24));
                        if (!volumes.TryGetValue(number, out var letters)) volumes[number] = letters = [];
                        if (!letters.Contains(letter)) letters.Add(letter);
                    }
                    break;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        return volumes.ToDictionary(p => p.Key, p => string.Join(' ', p.Value));
    }

    private static SafeFileHandle OpenDevice(string path) => CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);

    public void Dispose()
    {
        foreach (var device in _devices.Values) device.Handle.Dispose();
        _devices.Clear();
    }

    private sealed class Device(string path, SafeFileHandle handle)
    {
        internal string Path { get; } = path;
        internal SafeFileHandle Handle { get; } = handle;
        internal DiskPerformance? Previous { get; set; }
        internal long Timestamp { get; set; }
        internal string Volumes { get; set; } = string.Empty;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DiskPerformance
    {
        internal long BytesRead, BytesWritten, ReadTime, WriteTime, IdleTime;
        internal uint ReadCount, WriteCount, QueueDepth, SplitCount;
        internal long QueryTime;
        internal uint StorageDeviceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)] internal string StorageManagerName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StorageDeviceNumber { public uint Type, Number, Partition; }
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData { public uint Size; public Guid ClassGuid; public uint Flags; public IntPtr Reserved; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DeviceIoControl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadPerformance(SafeFileHandle device, uint code, IntPtr input, uint inputSize, out DiskPerformance output, uint outputSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DeviceIoControl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadDeviceNumber(SafeFileHandle device, uint code, IntPtr input, uint inputSize, out StorageDeviceNumber output, uint outputSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DeviceIoControl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadVolumeExtents(SafeFileHandle device, uint code, IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetLogicalDrives();
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr infoSet, IntPtr device, ref Guid classGuid, uint index, ref DeviceInterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr infoSet, ref DeviceInterfaceData data, IntPtr detail, uint size, out uint requiredSize, IntPtr deviceInfo);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr infoSet);
}
