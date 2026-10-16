using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CyberManager.Common.I18n;
using CyberManager.Core.Engine;
using CyberManager.UI.Controls;

namespace CyberManager.UI.Views;

public partial class DiskPerformanceView : UserControl
{
    private DiskTelemetrySnapshot _snapshot = new();
    private DiskMetrics[] _knownDisks = [];
    private bool _changingSelector;
    private sealed record DiskChoice(string Id, string Label);

    public DiskPerformanceView()
    {
        InitializeComponent();
        RefreshLocalization();
        IsVisibleChanged += (_, _) => { if (IsVisible) RenderMetrics(); };
    }

    public void RefreshLocalization()
    {
        Heading.Text = Strings.T("Disks");
        ActiveHistoryLabel.Text = Strings.T("DiskActiveTime");
        TransferLabel.Text = Strings.T("DiskTransferRate");
        ReadLegend.Text = ReadLabel.Text = Strings.T("DiskReadSpeed");
        WriteLegend.Text = WriteLabel.Text = Strings.T("DiskWriteSpeed");
        ResponseLabel.Text = Strings.T("DiskResponseTime");
        QueueLabel.Text = Strings.T("DiskQueueLength");
        TotalReadLabel.Text = Strings.T("DiskTotalRead");
        TotalWriteLabel.Text = Strings.T("DiskTotalWritten");
        OperationsLabel.Text = Strings.T("DiskTransfers");
        DiskCountLabel.Text = Strings.T("DiskCount");
        HistoryHint.Text = Strings.T("DiskHistoryHint");
        AutomationProperties.SetName(DiskSelector, Strings.T("DiskSelection"));
        TransferGraph.PrimarySeriesLabel = Strings.T("DiskReadSpeed");
        TransferGraph.SecondarySeriesLabel = Strings.T("DiskWriteSpeed");
        RebuildSelector();
        RenderMetrics();
    }

    public void UpdateMetrics(DiskTelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;
        if (IsVisible) RenderMetrics();
    }

    private void RebuildSelector()
    {
        var selected = DiskSelector.SelectedValue as string ?? string.Empty;
        if (_snapshot.Status == DiskMetricsStatus.Ready) _knownDisks = _snapshot.Disks.ToArray();
        var choices = new List<DiskChoice> { new(string.Empty, Strings.T("AllDisksCombined")) };
        choices.AddRange(_knownDisks.Select(disk =>
        {
            return new DiskChoice(disk.Id, Strings.T("DiskNumber", disk.Id) +
                (disk.VolumeNames.Length > 0 ? $" ({disk.VolumeNames})" : string.Empty));
        }));
        _changingSelector = true;
        try
        {
            DiskSelector.ItemsSource = choices;
            DiskSelector.SelectedValue = choices.Any(c => c.Id == selected) ? selected : string.Empty;
        }
        finally { _changingSelector = false; }
    }

    private void DiskSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_changingSelector && Graphs != null) RenderMetrics();
    }

    private void RenderMetrics()
    {
        if (_snapshot.Status == DiskMetricsStatus.Ready &&
            !_knownDisks.Select(d => (d.Id, d.VolumeNames)).SequenceEqual(_snapshot.Disks.Select(d => (d.Id, d.VolumeNames)))) RebuildSelector();
        var ready = _snapshot.Status == DiskMetricsStatus.Ready;
        Graphs.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = Strings.T(_snapshot.Status == DiskMetricsStatus.WarmingUp ? "DiskWarmingUp" : "DiskUnavailable");
        var id = DiskSelector.SelectedValue as string ?? string.Empty;
        var disk = id.Length == 0 ? _snapshot.Combined : _snapshot.Disks.FirstOrDefault(d => d.Id == id);
        ScopeHint.Text = Strings.T(id.Length == 0 ? "DiskCombinedHint" : "DiskSelectedHint");
        ActiveValue.Text = ready && disk != null ? $"{disk.ActivePercent:F1}%" : "—";
        if (!ready || disk == null)
        {
            ReadValue.Text = WriteValue.Text = ResponseValue.Text = QueueValue.Text = TotalReadValue.Text =
                TotalWriteValue.Text = OperationsValue.Text = DiskCountValue.Text = "—";
            return;
        }

        ActiveGraph.CurrentValue = disk.ActivePercent;
        ActiveGraph.UpdateHistory(disk.ActiveHistory, null, disk, DiskMetricsCollector.SampleInterval);
        var read = disk.ReadHistory.Select(value => value / 1_000_000).ToArray();
        var write = disk.WriteHistory.Select(value => value / 1_000_000).ToArray();
        var peak = Math.Max(read.DefaultIfEmpty().Max(), write.DefaultIfEmpty().Max());
        var scale = Math.Pow(2, Math.Ceiling(Math.Log2(Math.Max(1, peak * 1.1))));
        TransferGraph.MaxValue = scale;
        TransferScale.Text = $"{scale:0.#} MB/s";
        TransferGraph.UpdateHistory(read, write, disk, DiskMetricsCollector.SampleInterval);
        ReadValue.Text = DiskMetricFormatting.Rate(disk.ReadBytesPerSecond);
        WriteValue.Text = DiskMetricFormatting.Rate(disk.WriteBytesPerSecond);
        ResponseValue.Text = disk.ResponseMilliseconds is { } response ? $"{response:F2} ms" : "—";
        QueueValue.Text = disk.QueueLength is { } queue ? $"{queue:0.##}" : "—";
        TotalReadValue.Text = disk.TotalReadBytes is { } totalRead ? DiskMetricFormatting.Bytes(totalRead) : "—";
        TotalWriteValue.Text = disk.TotalWrittenBytes is { } totalWrite ? DiskMetricFormatting.Bytes(totalWrite) : "—";
        OperationsValue.Text = disk.TransfersPerSecond is { } transfers ? $"{transfers:0.#}/s" : "—";
        DiskCountValue.Text = (id.Length == 0 ? _snapshot.Disks.Count : 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
