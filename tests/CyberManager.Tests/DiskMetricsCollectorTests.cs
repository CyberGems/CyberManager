using CyberManager.Core.Engine;
using Xunit;

namespace CyberManager.Tests;

public class DiskMetricsCollectorTests
{
    [Fact]
    public void Combined_AveragesUtilizationAndWeightsLatencyByTransfers()
    {
        var combined = DiskMetrics.Combine([
            new() { ActivePercent = 100, ReadBytesPerSecond = 100, WriteBytesPerSecond = 50, TransfersPerSecond = 10,
                ResponseMilliseconds = 10, QueueLength = 2, TotalReadBytes = 1000, TotalWrittenBytes = 2000 },
            new() { ActivePercent = 0, ReadBytesPerSecond = 200, WriteBytesPerSecond = 70, TransfersPerSecond = 90,
                ResponseMilliseconds = 1, QueueLength = 3, TotalReadBytes = 3000, TotalWrittenBytes = 4000 }
        ]);
        Assert.Equal(50, combined.ActivePercent);
        Assert.Equal(300, combined.ReadBytesPerSecond);
        Assert.Equal(120, combined.WriteBytesPerSecond);
        Assert.Equal(100, combined.TransfersPerSecond);
        Assert.Equal(1.9, combined.ResponseMilliseconds!.Value, 5);
        Assert.Equal(5, combined.QueueLength);
        Assert.Equal(4000, combined.TotalReadBytes);
        Assert.Equal(6000, combined.TotalWrittenBytes);
    }

    [Fact]
    public void Combined_DoesNotReplaceUnavailableOptionalMetricsWithZero()
    {
        var combined = DiskMetrics.Combine([new() { TotalReadBytes = 200 }, new()]);
        Assert.Null(combined.TotalReadBytes);
        Assert.Null(combined.TotalWrittenBytes);
        Assert.Null(combined.QueueLength);
        Assert.Null(combined.ResponseMilliseconds);
    }

    [Fact]
    public void SessionTotals_UseRawByteDeltasAndSurviveCounterResets()
    {
        using var collector = new DiskMetricsCollector(new FakeSource(
            Ready(new DiskCounterReading("0 C:", 75, 10, 20, ReadBytes: 1000, WrittenBytes: 2000)),
            Ready(new DiskCounterReading("0 C:", 50, 30, 40, ReadBytes: 1300, WrittenBytes: 2500)),
            Ready(new DiskCounterReading("0 C:", 90, 0, 0, ReadBytes: 10, WrittenBytes: 20)),
            Ready(new DiskCounterReading("0 C:", 80, 0, 0, ReadBytes: 30, WrittenBytes: 50))));
        Assert.Equal(0, collector.Sample().Combined.TotalReadBytes);
        var second = collector.Sample();
        Assert.Equal(300, second.Combined.TotalReadBytes);
        Assert.Equal(500, second.Combined.TotalWrittenBytes);
        Assert.Equal(new float[] { 25, 50 }, second.Combined.ActiveHistory);
        var reset = collector.Sample();
        Assert.Equal(300, reset.Combined.TotalReadBytes);
        var last = collector.Sample();
        Assert.Equal(320, last.Combined.TotalReadBytes);
        Assert.Equal(530, last.Combined.TotalWrittenBytes);
    }

    [Theory]
    [InlineData(DiskMetricsStatus.WarmingUp, 0u)]
    [InlineData(DiskMetricsStatus.Unavailable, 0x800007D1u)]
    public void SamplingGap_ClearsHistoryAndExposesStatus(DiskMetricsStatus status, uint errorCode)
    {
        using var collector = new DiskMetricsCollector(new FakeSource(
            Ready(new DiskCounterReading("0 C:", 60, 100, 0, ReadBytes: 1000)),
            new(status, [], errorCode),
            Ready(new DiskCounterReading("0 C:", 80, 200, 0, ReadBytes: 3000))));
        collector.Sample();
        var gap = collector.Sample();
        Assert.Equal(status, gap.Status);
        Assert.Equal(errorCode, gap.ErrorCode);
        Assert.Empty(gap.Disks);
        Assert.Empty(gap.Combined.ActiveHistory);
        var recovered = collector.Sample();
        Assert.Single(recovered.Combined.ActiveHistory);
        Assert.Equal(2000, recovered.Combined.TotalReadBytes);
    }

    [Fact]
    public void Hotplug_RemovesDisconnectedDiskAndBaselinesNewDisk()
    {
        using var collector = new DiskMetricsCollector(new FakeSource(
            Ready(new("0 C:", 50, 100, 0, ReadBytes: 1000), new("1 D:", 0, 200, 0, ReadBytes: 2000)),
            Ready(new DiskCounterReading("0 C:", 75, 50, 0, ReadBytes: 1500)),
            Ready(new("0 C:", 75, 50, 0, ReadBytes: 1800), new("1 E:", 90, 10, 0, ReadBytes: 10000))));
        Assert.Equal(2, collector.Sample().Disks.Count);
        Assert.Single(collector.Sample().Disks);
        var reconnected = collector.Sample();
        Assert.Equal(2, reconnected.Disks.Count);
        Assert.Equal(0, reconnected.Disks[1].TotalReadBytes);
        Assert.Single(reconnected.Disks[1].ReadHistory);
        Assert.Equal(800, reconnected.Combined.TotalReadBytes);
    }

    [Fact]
    public void Histories_AreBoundedAndEarlierSnapshotsStayUnchanged()
    {
        var samples = Enumerable.Range(0, 150).Select(i => Ready(new DiskCounterReading("0 C:", 50, i, i * 2))).ToArray();
        using var collector = new DiskMetricsCollector(new FakeSource(samples));
        var first = collector.Sample();
        DiskTelemetrySnapshot latest = first;
        for (var i = 1; i < samples.Length; i++) latest = collector.Sample();
        Assert.Single(first.Combined.ReadHistory);
        Assert.Equal(DiskMetricsCollector.HistoryCapacity, latest.Combined.ReadHistory.Length);
        Assert.Equal(30, latest.Combined.ReadHistory[0]);
        Assert.Equal(149, latest.Combined.ReadHistory[^1]);
    }

    [Fact]
    public void Dispose_ReleasesSourceOnceAndRejectsFurtherSampling()
    {
        var source = new FakeSource();
        var collector = new DiskMetricsCollector(source);
        collector.Dispose();
        collector.Dispose();
        Assert.Equal(1, source.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => collector.Sample());
    }

    [Fact]
    public void NativeDeltas_ConvertHundredNanosecondsAndUseActualElapsedTime()
    {
        var previous = new NativeDiskPerformanceSource.DiskPerformance
        { BytesRead = 1000, BytesWritten = 2000, IdleTime = 1_000_000, ReadTime = 100_000, WriteTime = 200_000, ReadCount = 10, WriteCount = 5 };
        var current = new NativeDiskPerformanceSource.DiskPerformance
        { BytesRead = 5000, BytesWritten = 10000, IdleTime = 3_500_000, ReadTime = 200_000, WriteTime = 300_000, ReadCount = 12, WriteCount = 7, QueueDepth = 3 };
        var sample = NativeDiskPerformanceSource.Calculate("0", previous, current, 0.5, "C:")!;
        Assert.Equal(50, sample.IdlePercent);
        Assert.Equal(8000, sample.ReadBytesPerSecond);
        Assert.Equal(16000, sample.WriteBytesPerSecond);
        Assert.Equal(8, sample.TransfersPerSecond);
        Assert.Equal(5, sample.ResponseMilliseconds);
        Assert.Equal(3, sample.QueueLength);
        Assert.Equal("C:", sample.VolumeNames);
        Assert.Equal(88, System.Runtime.InteropServices.Marshal.SizeOf<NativeDiskPerformanceSource.DiskPerformance>());
    }

    [Fact]
    public void NativeDeltas_HandleOperationCounterRollover()
    {
        var previous = new NativeDiskPerformanceSource.DiskPerformance { ReadCount = uint.MaxValue };
        var current = new NativeDiskPerformanceSource.DiskPerformance { ReadCount = 1, ReadTime = 10000, BytesRead = 100 };
        var sample = NativeDiskPerformanceSource.Calculate("0", previous, current, 0.5)!;
        Assert.Equal(4, sample.TransfersPerSecond);
        Assert.Equal(0.5, sample.ResponseMilliseconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(double.NaN)]
    public void NativeDeltas_RejectInvalidOrStaleIntervals(double seconds)
    {
        Assert.Null(NativeDiskPerformanceSource.Calculate("0", default, default, seconds));
    }

    [Fact]
    public void NativeDeltas_RebaselineAfterDeviceCounterReset()
    {
        var previous = new NativeDiskPerformanceSource.DiskPerformance { BytesRead = 1000 };
        var current = new NativeDiskPerformanceSource.DiskPerformance { BytesRead = 1 };
        Assert.Null(NativeDiskPerformanceSource.Calculate("0", previous, current, 0.5));
    }

    [Fact]
    public void EmptyDeviceList_IsUnavailableInsteadOfIdle()
    {
        using var collector = new DiskMetricsCollector(new FakeSource(Ready()));
        Assert.Equal(DiskMetricsStatus.Unavailable, collector.Sample().Status);
    }

    private static DiskCounterSample Ready(params DiskCounterReading[] disks) => new(DiskMetricsStatus.Ready, disks);

    private sealed class FakeSource(params DiskCounterSample[] samples) : IDiskPerformanceSource
    {
        private int _index;
        internal int DisposeCount { get; private set; }
        public DiskCounterSample Read() => samples[_index++];
        public void Dispose() => DisposeCount++;
    }
}
