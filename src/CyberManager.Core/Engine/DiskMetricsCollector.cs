namespace CyberManager.Core.Engine;

public enum DiskMetricsStatus { WarmingUp, Ready, Unavailable }

public sealed record DiskMetrics
{
    public string Id { get; init; } = string.Empty;
    public string VolumeNames { get; init; } = string.Empty;
    public double ActivePercent { get; init; }
    public double ReadBytesPerSecond { get; init; }
    public double WriteBytesPerSecond { get; init; }
    public double? TransfersPerSecond { get; init; }
    public double? ResponseMilliseconds { get; init; }
    public double? QueueLength { get; init; }
    public long? TotalReadBytes { get; init; }
    public long? TotalWrittenBytes { get; init; }
    public float[] ActiveHistory { get; init; } = [];
    public float[] ReadHistory { get; init; } = [];
    public float[] WriteHistory { get; init; } = [];

    internal static DiskMetrics Combine(IReadOnlyList<DiskMetrics> disks)
    {
        if (disks.Count == 0) return new();
        var timed = disks.Where(d => d.ResponseMilliseconds.HasValue && d.TransfersPerSecond is > 0).ToArray();
        var operations = timed.Sum(d => d.TransfersPerSecond!.Value);
        return new DiskMetrics
        {
            // Utilization is an average, while transfer rates, queues and byte totals are sums.
            ActivePercent = disks.Average(d => d.ActivePercent),
            ReadBytesPerSecond = disks.Sum(d => d.ReadBytesPerSecond),
            WriteBytesPerSecond = disks.Sum(d => d.WriteBytesPerSecond),
            TransfersPerSecond = disks.All(d => d.TransfersPerSecond.HasValue) ? disks.Sum(d => d.TransfersPerSecond!.Value) : null,
            ResponseMilliseconds = operations > 0
                ? timed.Sum(d => d.ResponseMilliseconds!.Value * d.TransfersPerSecond!.Value) / operations : null,
            QueueLength = disks.All(d => d.QueueLength.HasValue) ? disks.Sum(d => d.QueueLength!.Value) : null,
            TotalReadBytes = disks.All(d => d.TotalReadBytes.HasValue) ? disks.Sum(d => d.TotalReadBytes!.Value) : null,
            TotalWrittenBytes = disks.All(d => d.TotalWrittenBytes.HasValue) ? disks.Sum(d => d.TotalWrittenBytes!.Value) : null
        };
    }
}

public sealed record DiskTelemetrySnapshot
{
    public DiskMetricsStatus Status { get; init; } = DiskMetricsStatus.WarmingUp;
    public uint ErrorCode { get; init; }
    public DiskMetrics Combined { get; init; } = new();
    public IReadOnlyList<DiskMetrics> Disks { get; init; } = Array.Empty<DiskMetrics>();
}

/// <summary>Owns the native disk source and independent, bounded histories for each physical disk.</summary>
public sealed class DiskMetricsCollector : IDisposable
{
    public const int HistoryCapacity = 120;
    public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);
    private readonly object _gate = new();
    private readonly IDiskPerformanceSource _source;
    private readonly Dictionary<string, DiskState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly DiskState _combined = new();
    private bool _disposed;

    public DiskMetricsCollector() : this(new NativeDiskPerformanceSource()) { }
    internal DiskMetricsCollector(IDiskPerformanceSource source) => _source = source;

    public DiskTelemetrySnapshot Sample()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var reading = _source.Read();
            if (reading.Status != DiskMetricsStatus.Ready || reading.Disks.Count == 0)
            {
                // A hidden period or counter failure is a gap, not a stretch of zero activity.
                foreach (var state in _states.Values) state.History.Clear();
                _combined.History.Clear();
                return new()
                {
                    Status = reading.Status == DiskMetricsStatus.Ready ? DiskMetricsStatus.Unavailable : reading.Status,
                    ErrorCode = reading.ErrorCode
                };
            }

            var ids = reading.Disks.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _states.Keys.Where(id => !ids.Contains(id)).ToArray()) _states.Remove(id);
            var disks = new List<DiskMetrics>();
            foreach (var disk in reading.Disks)
            {
                if (!_states.TryGetValue(disk.Id, out var state)) _states[disk.Id] = state = new();
                var metrics = new DiskMetrics
                {
                    Id = disk.Id,
                    VolumeNames = disk.VolumeNames ?? string.Empty,
                    ActivePercent = Math.Clamp(100 - disk.IdlePercent, 0, 100),
                    ReadBytesPerSecond = disk.ReadBytesPerSecond,
                    WriteBytesPerSecond = disk.WriteBytesPerSecond,
                    TransfersPerSecond = disk.TransfersPerSecond,
                    ResponseMilliseconds = disk.ResponseMilliseconds,
                    QueueLength = disk.QueueLength,
                    TotalReadBytes = state.UpdateReadTotal(disk.ReadBytes),
                    TotalWrittenBytes = state.UpdateWriteTotal(disk.WrittenBytes)
                };
                disks.Add(state.WithHistory(metrics));
            }
            return new()
            {
                Status = DiskMetricsStatus.Ready,
                Disks = disks,
                Combined = _combined.WithHistory(DiskMetrics.Combine(disks))
            };
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _source.Dispose();
        }
    }

    private sealed class DiskState
    {
        private long? _lastRead;
        private long? _lastWrite;
        private long _totalRead;
        private long _totalWrite;
        internal Queue<DiskMetrics> History { get; } = new();

        internal long? UpdateReadTotal(long? value) => Accumulate(value, ref _lastRead, ref _totalRead);
        internal long? UpdateWriteTotal(long? value) => Accumulate(value, ref _lastWrite, ref _totalWrite);
        private static long? Accumulate(long? value, ref long? previous, ref long total)
        {
            if (value is not { } current) return null;
            if (previous is { } last && current >= last) total += current - last;
            previous = current; // Counter resets establish a new baseline without adding a negative delta.
            return total;
        }

        internal DiskMetrics WithHistory(DiskMetrics sample)
        {
            History.Enqueue(sample);
            if (History.Count > HistoryCapacity) History.Dequeue();
            return sample with
            {
                ActiveHistory = History.Select(d => (float)d.ActivePercent).ToArray(),
                ReadHistory = History.Select(d => (float)d.ReadBytesPerSecond).ToArray(),
                WriteHistory = History.Select(d => (float)d.WriteBytesPerSecond).ToArray()
            };
        }
    }
}

internal sealed record DiskCounterReading(
    string Id, double IdlePercent, double ReadBytesPerSecond, double WriteBytesPerSecond,
    double? TransfersPerSecond = null, double? ResponseMilliseconds = null, double? QueueLength = null,
    long? ReadBytes = null, long? WrittenBytes = null, string? VolumeNames = null);

internal sealed record DiskCounterSample(DiskMetricsStatus Status, IReadOnlyList<DiskCounterReading> Disks, uint ErrorCode = 0);

internal interface IDiskPerformanceSource : IDisposable
{
    DiskCounterSample Read();
}
