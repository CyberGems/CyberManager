using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CyberManager.Common.I18n;
using CyberManager.Core.Engine;

namespace CyberManager.UI.Views;

public partial class PerformanceView : UserControl
{
    private bool _isActive;

    public PerformanceView()
    {
        InitializeComponent();
        RefreshLocalization();

        Loaded += (_, _) =>
        {
            if (_isActive) UpdateMetrics();
        };
    }

    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        UpdateMetrics();
    }

    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
    }

    public void SelectSubTab(string name)
    {
        TabSummary.IsChecked = string.Equals(name, "Summary", StringComparison.OrdinalIgnoreCase);
        TabCpu.IsChecked = string.Equals(name, "Cpu", StringComparison.OrdinalIgnoreCase);
        TabMemory.IsChecked = string.Equals(name, "Memory", StringComparison.OrdinalIgnoreCase);
        TabIo.IsChecked = string.Equals(name, "Io", StringComparison.OrdinalIgnoreCase);
        TabDisks.IsChecked = string.Equals(name, "Disks", StringComparison.OrdinalIgnoreCase);
        UpdateSubTabVisibility();
    }

    public void RefreshLocalization()
    {
        // Tab content now carries an icon next to the label, so localization writes the
        // TextBlock leaves instead of replacing the whole Content (that would drop the icons).
        TabSummaryLabel.Text = Strings.T("Summary");
        TabCpuLabel.Text = Strings.T("Cpu");
        TabMemoryLabel.Text = Strings.T("Memory");
        TabIoLabel.Text = Strings.T("IoHistory");
        TabDisksLabel.Text = Strings.T("Disks");
        AutomationProperties.SetName(TabSummary, Strings.T("Summary"));
        AutomationProperties.SetName(TabCpu, Strings.T("Cpu"));
        AutomationProperties.SetName(TabMemory, Strings.T("Memory"));
        AutomationProperties.SetName(TabIo, Strings.T("IoHistory"));
        AutomationProperties.SetName(TabDisks, Strings.T("Disks"));
        ViewDisks.RefreshLocalization();

        CpuHistoryTitle.Text = Strings.T("CpuHistory");
        CpuLegend.Text = Strings.T("CpuLegend");
        MemHistoryTitle.Text = Strings.T("MemoryHistory");
        MemLegend.Text = Strings.T("MemoryLegend");
        TotalsCardTitle.Text = Strings.T("Totals");
        HandlesLbl.Text = Strings.T("Handles");
        ThreadsLbl.Text = Strings.T("Threads");
        ProcessesLbl.Text = Strings.T("Processes");

        CpuCardTitle.Text = Strings.T("Cpu");
        CpuTotalLbl.Text = Strings.T("CpuTotalLabel");
        CpuKernelLbl.Text = Strings.T("CpuKernelLabel");
        CpuUserLbl.Text = Strings.T("CpuUserLabel");

        MemCardTitle.Text = $"{Strings.T("Memory")} (GB)";
        MemInUseLbl.Text = Strings.T("InUseMemory");
        MemAvailLbl.Text = Strings.T("AvailableMemory");
        MemTotalLbl.Text = Strings.T("TotalMemory");

        TopologyCardTitle.Text = Strings.T("Topology");
        CoresLbl.Text = Strings.T("Cores");
        SocketsLbl.Text = Strings.T("Sockets");
        LogProcLbl.Text = Strings.T("LogicalProcessors");

        FullCpuHistoryTitle.Text = Strings.T("CpuActivityHistory");
        FullCpuMetricsTitle.Text = Strings.T("CpuMetrics");
        ProcessorTopologyTitle.Text = Strings.T("ProcessorTopology");
        FullMemHistoryTitle.Text = Strings.T("PhysicalMemoryCommitHistory");
        PhysicalMemoryTitle.Text = Strings.T("PhysicalMemoryGb");
        CommitChargeTitle.Text = Strings.T("CommitCharge");
        CommitTotalLbl.Text = Strings.T("CommittedTotal");
        CommitLimitLbl.Text = Strings.T("CommitLimit");
        CommitPeakLbl.Text = Strings.T("CommitPeak");
        KernelPoolTitle.Text = Strings.T("KernelPool");
        PagedPoolLbl.Text = Strings.T("PagedPool");
        NonPagedPoolLbl.Text = Strings.T("NonPagedPool");
        FullHandlesLbl.Text = Strings.T("TotalHandles");

        IoTitle.Text = Strings.T("SystemActivityOverview");
        IoSystemTitle.Text = Strings.T("SystemOverviewTitle");
        IoProcessesLbl.Text = Strings.T("TotalActiveProcesses");
        IoThreadsLbl.Text = Strings.T("Threads");
        IoHandlesLbl.Text = Strings.T("Handles");
        IoUptimeLbl.Text = Strings.T("SystemUptime");
        IoKernelTitle.Text = Strings.T("KernelPool");
        IoPagedLbl.Text = Strings.T("PagedPool");
        IoNonPagedLbl.Text = Strings.T("NonPagedPool");
        IoCacheLbl.Text = Strings.T("SystemFileCache");
        IoCommitTitle.Text = Strings.T("CommitCharge");
        IoCommitTotalLbl.Text = Strings.T("CommitTotalLabel");
        IoCommitLimitLbl.Text = Strings.T("CommitLimitLabel");
        IoCommitPeakLbl.Text = Strings.T("CommitPeakLabel");
        IoPhysicalTitle.Text = Strings.T("PhysicalMemory");
        IoInUseLbl.Text = Strings.T("InUseMemory");
        IoAvailLbl.Text = Strings.T("AvailableMemory");
        IoLoadLbl.Text = Strings.T("MemoryLoadPercent");
        TelemetryFooter.Text = Strings.T("TelemetryFooter");
    }

    public void UpdateMetrics()
    {
        if (!_isActive) return;
        var snap = SystemMetricsCollector.Instance.Latest;
        var (cpuTotal, cpuKernel) = SystemMetricsCollector.Instance.GetCpuHistory();
        var (_, ramPct) = SystemMetricsCollector.Instance.GetRamHistory();
        var commitGb = SystemMetricsCollector.Instance.GetCommitHistory();
        var commitPct = snap.CommitLimitGb > 0
            ? commitGb.Select(value => (float)(value / snap.CommitLimitGb * 100.0)).ToArray()
            : Array.Empty<float>();

        var refreshInterval = TimeSpan.FromMilliseconds(Math.Max(500, App.Settings.RefreshIntervalMs));

        // 1. Hardware Overview Chip
        CpuModelSubtitle.Text = $"{snap.CpuModelName} • {snap.PhysicalCores} Cores / {snap.LogicalProcessors} Threads • {snap.TotalRamGb:F1} GB RAM";

        // 2. Summary Tab Graphs
        SummaryCpuGraph.CurrentValue = snap.CpuTotalPercent;
        SummaryCpuGraph.SecondaryCurrentValue = snap.CpuKernelPercent;
        SummaryCpuGraph.UpdateHistory(cpuTotal, cpuKernel, snap, refreshInterval);

        SummaryMemGraph.CurrentValue = snap.MemoryLoadPercent;
        SummaryMemGraph.SecondaryCurrentValue = snap.CommitLimitGb > 0 ? (snap.CommitTotalGb / snap.CommitLimitGb) * 100.0 : 0.0;
        SummaryMemGraph.UpdateHistory(ramPct, commitPct, snap, refreshInterval);

        // 3. Summary Tab Cards
        HandlesVal.Text = $"{snap.HandleCount:N0}";
        ThreadsVal.Text = $"{snap.ThreadCount:N0}";
        ProcessesVal.Text = $"{snap.ProcessCount:N0}";

        CpuTotalVal.Text = $"{snap.CpuTotalPercent:F1}%";
        CpuKernelVal.Text = $"{snap.CpuKernelPercent:F1}%";
        CpuUserVal.Text = $"{snap.CpuUserPercent:F1}%";

        MemInUseVal.Text = $"{snap.UsedRamGb:F1} GB";
        MemAvailVal.Text = $"{snap.AvailableRamGb:F1} GB";
        MemTotalVal.Text = $"{snap.TotalRamGb:F1} GB";

        CoresVal.Text = snap.PhysicalCores.ToString(CultureInfo.CurrentCulture);
        SocketsVal.Text = snap.Sockets.ToString(CultureInfo.CurrentCulture);
        LogProcVal.Text = snap.LogicalProcessors.ToString(CultureInfo.CurrentCulture);

        // 4. Full CPU Tab
        FullCpuGraph.CurrentValue = snap.CpuTotalPercent;
        FullCpuGraph.SecondaryCurrentValue = snap.CpuKernelPercent;
        FullCpuGraph.UpdateHistory(cpuTotal, cpuKernel, snap, refreshInterval);

        FullCpuTotal.Text = $"{snap.CpuTotalPercent:F1}%";
        FullCpuKernel.Text = $"{snap.CpuKernelPercent:F1}%";
        FullCpuUser.Text = $"{snap.CpuUserPercent:F1}%";
        FullCpuModel.Text = snap.CpuModelName;
        FullCpuCores.Text = snap.PhysicalCores.ToString(CultureInfo.CurrentCulture);
        FullCpuLogical.Text = snap.LogicalProcessors.ToString(CultureInfo.CurrentCulture);

        // 5. Full Memory Tab
        FullMemGraph.CurrentValue = snap.MemoryLoadPercent;
        FullMemGraph.SecondaryCurrentValue = snap.CommitLimitGb > 0 ? (snap.CommitTotalGb / snap.CommitLimitGb) * 100.0 : 0.0;
        FullMemGraph.UpdateHistory(ramPct, commitPct, snap, refreshInterval);

        FullMemTotal.Text = $"{snap.TotalRamGb:F1} GB";
        FullMemInUse.Text = $"{snap.UsedRamGb:F1} GB ({snap.MemoryLoadPercent:F0}%)";
        FullMemAvail.Text = $"{snap.AvailableRamGb:F1} GB";

        FullCommitTotal.Text = $"{snap.CommitTotalGb:F1} GB";
        FullCommitLimit.Text = $"{snap.CommitLimitGb:F1} GB";
        FullCommitPeak.Text = $"{snap.CommitPeakGb:F1} GB";

        FullPagedPool.Text = $"{snap.PagedPoolMb:F0} MB";
        FullNonPagedPool.Text = $"{snap.NonPagedPoolMb:F0} MB";
        FullHandles.Text = $"{snap.HandleCount:N0}";

        // 6. I/O Tab
        IoProcessesVal.Text = $"{snap.ProcessCount:N0}";
        IoThreadsVal.Text = $"{snap.ThreadCount:N0}";
        IoHandlesVal.Text = $"{snap.HandleCount:N0}";
        IoUptimeVal.Text = FormatUptime(SystemMetricsSnapshot.SystemUptime);
        IoPagedVal.Text = $"{snap.PagedPoolMb:F0} MB";
        IoNonPagedVal.Text = $"{snap.NonPagedPoolMb:F0} MB";
        IoCacheVal.Text = $"{snap.SystemCacheMb:F0} MB";
        IoCommitTotalVal.Text = $"{snap.CommitTotalGb:F2} GB";
        IoCommitLimitVal.Text = $"{snap.CommitLimitGb:F2} GB";
        IoCommitPeakVal.Text = $"{snap.CommitPeakGb:F2} GB";
        IoInUseVal.Text = $"{snap.UsedRamGb:F2} GB";
        IoAvailVal.Text = $"{snap.AvailableRamGb:F2} GB";
        IoLoadVal.Text = $"{snap.MemoryLoadPercent:F0}%";
    }

    private void SubTab_Checked(object sender, RoutedEventArgs e)
    {
        UpdateSubTabVisibility();
    }

    private void UpdateSubTabVisibility()
    {
        if (ViewSummary == null || ViewCpu == null || ViewMemory == null || ViewIo == null || ViewDisks == null) return;

        ViewSummary.Visibility = TabSummary.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewCpu.Visibility = TabCpu.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewMemory.Visibility = TabMemory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewIo.Visibility = TabIo.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewDisks.Visibility = TabDisks.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TelemetryStatus.Visibility = TabDisks.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }

    public void UpdateDiskMetrics(DiskTelemetrySnapshot snapshot) => ViewDisks.UpdateMetrics(snapshot);

    /// <summary>Compact up time: "3d 04h 12m", "4h 12m" or "12m".</summary>
    private static string FormatUptime(TimeSpan uptime) => uptime.TotalDays >= 1
        ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
        : uptime.Hours > 0
            ? $"{uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m";
}
