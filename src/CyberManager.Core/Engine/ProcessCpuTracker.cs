using System.Diagnostics;
using CyberManager.Common.Models;

namespace CyberManager.Core.Engine;

/// <summary>
/// Calculates per-process CPU usage from two deterministic snapshots.
/// Process start time is part of the identity so a reused PID cannot inflate a delta.
/// </summary>
public sealed class ProcessCpuTracker
{
    private readonly Dictionary<ProcessIdentity, long> _previousCpuTicks = new();
    private long _previousTimestamp;

    public void Update(
        IList<ProcessInfo> processes,
        long timestamp,
        long timestampFrequency,
        int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);

        var elapsedTicks = _previousTimestamp > 0 ? timestamp - _previousTimestamp : 0;
        var elapsedSeconds = elapsedTicks > 0
            ? (double)elapsedTicks / timestampFrequency
            : 0;
        var cpuCount = Math.Max(1, processorCount);
        var currentCpuTicks = new Dictionary<ProcessIdentity, long>(processes.Count);

        foreach (var process in processes)
        {
            var identity = ProcessIdentity.From(process);
            currentCpuTicks[identity] = process.CpuTimeTicks;
            process.CpuPercent = 0;

            if (elapsedSeconds <= 0 ||
                !_previousCpuTicks.TryGetValue(identity, out var previousTicks) ||
                process.CpuTimeTicks < previousTicks)
            {
                continue;
            }

            var delta = process.CpuTimeTicks - previousTicks;
            var cpuSeconds = delta / 10_000_000.0;
            var percent = cpuSeconds / (elapsedSeconds * cpuCount) * 100.0;
            process.CpuPercent = double.IsFinite(percent)
                ? Math.Clamp(percent, 0, 100)
                : 0;
        }

        // The process list and system clock are sampled independently. Normalize
        // small sampling overshoots so the displayed process total cannot exceed 100%.
        var total = processes.Sum(process => process.CpuPercent);
        if (total > 100 && double.IsFinite(total))
        {
            var scale = 100 / total;
            foreach (var process in processes)
            {
                process.CpuPercent *= scale;
            }
        }

        _previousCpuTicks.Clear();
        foreach (var pair in currentCpuTicks)
        {
            _previousCpuTicks[pair.Key] = pair.Value;
        }

        _previousTimestamp = timestamp;
    }

    private readonly record struct ProcessIdentity(int Pid, long StartTimeFileTime)
    {
        public static ProcessIdentity From(ProcessInfo process)
        {
            var startTime = process.StartTime == default
                ? 0
                : process.StartTime.ToFileTimeUtc();
            return new ProcessIdentity(process.Pid, startTime);
        }
    }
}
