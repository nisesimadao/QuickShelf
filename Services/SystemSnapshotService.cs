using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickShelf.Services;

public sealed record ProcessSnapshot(int Pid, string Name, double CpuPercent, long MemoryBytes);
public sealed record PortSnapshot(int Port, int Pid, string ProcessName, string Label);
public sealed record GpuSnapshot(string Name, double UsagePercent, long MemoryUsedMb, long MemoryTotalMb);
public sealed record SystemSnapshot(
    double CpuPercent,
    double MemoryPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    GpuSnapshot? Gpu,
    IReadOnlyList<ProcessSnapshot> Processes,
    IReadOnlyList<PortSnapshot> Ports);

public sealed class SystemSnapshotService
{
    private readonly Dictionary<int, TimeSpan> _lastProcessCpu = new();
    private DateTime _lastProcessSampleUtc = DateTime.UtcNow;
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuSample;
    private int _gpuTick;
    private GpuSnapshot? _lastGpu;

    public async Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var cpu = SampleSystemCpu();
        var memory = SampleMemory();
        var processes = SampleProcesses();
        var ports = TcpPortReader.ReadListeningPorts();

        if ((_gpuTick++ & 1) == 0)
        {
            _lastGpu = await TrySampleNvidiaGpuAsync(cancellationToken).ConfigureAwait(false) ?? _lastGpu;
        }

        return new SystemSnapshot(
            cpu,
            memory.Percent,
            memory.Used,
            memory.Total,
            _lastGpu,
            processes,
            ports);
    }

    private double SampleSystemCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleValue = ToUInt64(idle);
        var kernelValue = ToUInt64(kernel);
        var userValue = ToUInt64(user);

        if (!_hasCpuSample)
        {
            _hasCpuSample = true;
            _lastIdle = idleValue;
            _lastKernel = kernelValue;
            _lastUser = userValue;
            return 0;
        }

        var idleDelta = idleValue - _lastIdle;
        var kernelDelta = kernelValue - _lastKernel;
        var userDelta = userValue - _lastUser;
        _lastIdle = idleValue;
        _lastKernel = kernelValue;
        _lastUser = userValue;

        var total = kernelDelta + userDelta;
        if (total == 0)
        {
            return 0;
        }

        return Math.Clamp((total - idleDelta) * 100.0 / total, 0, 100);
    }

    private static (double Percent, long Used, long Total) SampleMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status) || status.TotalPhysical == 0)
        {
            return (0, 0, 0);
        }

        var total = checked((long)status.TotalPhysical);
        var available = checked((long)status.AvailablePhysical);
        var used = Math.Max(0, total - available);
        return (used * 100.0 / total, used, total);
    }

    private IReadOnlyList<ProcessSnapshot> SampleProcesses()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Max(0.05, (now - _lastProcessSampleUtc).TotalSeconds);
        _lastProcessSampleUtc = now;
        var cpuCount = Math.Max(1, Environment.ProcessorCount);
        var current = new Dictionary<int, TimeSpan>();
        var snapshots = new List<ProcessSnapshot>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var pid = process.Id;
                var totalCpu = process.TotalProcessorTime;
                current[pid] = totalCpu;
                var hadPrevious = _lastProcessCpu.TryGetValue(pid, out var previousCpu);
                var cpuDelta = hadPrevious ? Math.Max(0, (totalCpu - previousCpu).TotalSeconds) : 0.0;
                var cpuPercent = hadPrevious
                    ? cpuDelta / elapsedSeconds / cpuCount * 100.0
                    : 0.0;

                snapshots.Add(new ProcessSnapshot(
                    pid,
                    string.IsNullOrWhiteSpace(process.ProcessName) ? $"PID {pid}" : process.ProcessName,
                    Math.Clamp(cpuPercent, 0, 100),
                    Math.Max(0, process.WorkingSet64)));
            }
            catch
            {
                // Processes can disappear between enumeration and sampling.
            }
            finally
            {
                process.Dispose();
            }
        }

        _lastProcessCpu.Clear();
        foreach (var pair in current)
        {
            _lastProcessCpu[pair.Key] = pair.Value;
        }

        // Keep enough real rows for the UI to actually scroll while still avoiding
        // hundreds of DOM nodes being rebuilt every second.
        return snapshots
            .OrderByDescending(x => x.CpuPercent)
            .ThenByDescending(x => x.MemoryBytes)
            .Take(48)
            .ToArray();
    }

    private static async Task<GpuSnapshot?> TrySampleNvidiaGpuAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    Arguments = "--query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var line = await outputTask.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var parts = line.Split(',').Select(x => x.Trim()).ToArray();
            if (parts.Length < 4
                || !double.TryParse(parts[1], out var usage)
                || !long.TryParse(parts[2], out var used)
                || !long.TryParse(parts[3], out var total))
            {
                return null;
            }

            return new GpuSnapshot(parts[0], Math.Clamp(usage, 0, 100), used, total);
        }
        catch
        {
            return null;
        }
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}

internal static class TcpPortReader
{
    private const int AddressFamilyInet = 2;
    private const int TcpTableOwnerPidListener = 3;
    private const int ErrorInsufficientBuffer = 122;

    public static IReadOnlyList<PortSnapshot> ReadListeningPorts()
    {
        var size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AddressFamilyInet, TcpTableOwnerPidListener, 0);
        if (result != ErrorInsufficientBuffer || size <= 0)
        {
            return Array.Empty<PortSnapshot>();
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(buffer, ref size, true, AddressFamilyInet, TcpTableOwnerPidListener, 0);
            if (result != 0)
            {
                return Array.Empty<PortSnapshot>();
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var ports = new List<PortSnapshot>(Math.Min(count, 32));

            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                rowPtr = IntPtr.Add(rowPtr, rowSize);

                var portBytes = BitConverter.GetBytes(row.LocalPort);
                var port = (portBytes[0] << 8) | portBytes[1];
                if (port <= 0 || port > 65535)
                {
                    continue;
                }

                var name = ResolveProcessName((int)row.OwningPid);
                ports.Add(new PortSnapshot(port, (int)row.OwningPid, name, FriendlyLabel(name, port)));
            }

            return ports
                .Where(x => x.Port >= 1024)
                .GroupBy(x => x.Port)
                .Select(x => x.First())
                .OrderBy(x => RankPort(x.Port))
                .ThenBy(x => x.Port)
                .Take(12)
                .ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ResolveProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return $"PID {pid}";
        }
    }

    private static string FriendlyLabel(string processName, int port)
    {
        var lower = processName.ToLowerInvariant();
        if (lower.Contains("node")) return port == 5173 ? "Vite / Node.js" : "Node.js";
        if (lower.Contains("python")) return "Python";
        if (lower.Contains("java")) return "Java";
        if (lower.Contains("postgres")) return "PostgreSQL";
        if (lower.Contains("redis")) return "Redis";
        return processName;
    }

    private static int RankPort(int port) => port switch
    {
        3000 or 3001 or 4173 or 5173 or 5174 or 8000 or 8080 or 8081 or 8888 => 0,
        5432 or 6379 or 3306 or 27017 => 1,
        _ => 2
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        int ipVersion,
        int tableClass,
        uint reserved);
}
