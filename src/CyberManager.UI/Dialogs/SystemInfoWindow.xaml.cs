using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Core.Engine;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class SystemInfoWindow : Window, IModalAttentionWindow
{
    private readonly DispatcherTimer _timer = new();
    private DateTime _lastAttentionTime = DateTime.MinValue;

    public void TriggerAttention()
    {
        if ((DateTime.UtcNow - _lastAttentionTime).TotalMilliseconds < 250) return;
        _lastAttentionTime = DateTime.UtcNow;

        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0.72,
            Duration = TimeSpan.FromMilliseconds(140),
            AutoReverse = true
        };
        OuterBorder.BeginAnimation(OpacityProperty, animation);
        Activate();
    }

    public SystemInfoWindow()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as System.Windows.Media.ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);

        RefreshLocalization();
        UpdateMetrics();

        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(500, App.Settings.RefreshIntervalMs));
        _timer.Tick += (_, _) => UpdateMetrics();
        _timer.Start();

        Closed += (_, _) => _timer.Stop();
    }

    public void RefreshLocalization()
    {
        Title = $"{Strings.T("SystemInformation")} ᐧ CyberManager";
        TitleText.Text = Strings.T("SystemInformation");
        // Tab content now carries an icon next to the label, so localization writes the
        // TextBlock leaves instead of replacing the whole Content (that would drop the icons).
        TabSummaryLabel.Text = Strings.T("Summary");
        TabCpuLabel.Text = Strings.T("Cpu");
        TabMemoryLabel.Text = Strings.T("Memory");
        TabIoLabel.Text = Strings.T("IoHistory");
        AutomationProperties.SetName(TabSummary, Strings.T("Summary"));
        AutomationProperties.SetName(TabCpu, Strings.T("Cpu"));
        AutomationProperties.SetName(TabMemory, Strings.T("Memory"));
        AutomationProperties.SetName(TabIo, Strings.T("IoHistory"));

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
    }

    public void UpdateMetrics()
    {
        var snap = SystemMetricsCollector.Instance.Latest;
        var (cpuTotal, cpuKernel) = SystemMetricsCollector.Instance.GetCpuHistory();
        var (_, ramPct) = SystemMetricsCollector.Instance.GetRamHistory();
        var commitGb = SystemMetricsCollector.Instance.GetCommitHistory();
        var commitPct = snap.CommitLimitGb > 0
            ? commitGb.Select(value => (float)(value / snap.CommitLimitGb * 100.0)).ToArray()
            : Array.Empty<float>();

        var refreshInterval = TimeSpan.FromMilliseconds(Math.Max(500, App.Settings.RefreshIntervalMs));
        if (_timer.Interval != refreshInterval) _timer.Interval = refreshInterval;

        // 1. Header Subtitle
        CpuModelSubtitle.Text = $"{snap.CpuModelName} • {snap.PhysicalCores} Cores / {snap.LogicalProcessors} Threads • {snap.TotalRamGb:F1} GB RAM";

        // 2. Summary Tab Graphs
        SummaryCpuGraph.CurrentValue = snap.CpuTotalPercent;
        SummaryCpuGraph.SecondaryCurrentValue = snap.CpuKernelPercent;
        SummaryCpuGraph.PrimaryValues = cpuTotal;
        SummaryCpuGraph.SecondaryValues = cpuKernel;

        SummaryMemGraph.CurrentValue = snap.MemoryLoadPercent;
        SummaryMemGraph.SecondaryCurrentValue = snap.CommitLimitGb > 0 ? (snap.CommitTotalGb / snap.CommitLimitGb) * 100.0 : 0.0;
        SummaryMemGraph.PrimaryValues = ramPct;
        SummaryMemGraph.SecondaryValues = commitPct;

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
        FullCpuGraph.PrimaryValues = cpuTotal;
        FullCpuGraph.SecondaryValues = cpuKernel;

        FullCpuTotal.Text = $"{snap.CpuTotalPercent:F1}%";
        FullCpuKernel.Text = $"{snap.CpuKernelPercent:F1}%";
        FullCpuUser.Text = $"{snap.CpuUserPercent:F1}%";
        FullCpuModel.Text = snap.CpuModelName;
        FullCpuCores.Text = snap.PhysicalCores.ToString(CultureInfo.CurrentCulture);
        FullCpuLogical.Text = snap.LogicalProcessors.ToString(CultureInfo.CurrentCulture);

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

        // 5. Full Memory Tab
        FullMemGraph.CurrentValue = snap.MemoryLoadPercent;
        FullMemGraph.SecondaryCurrentValue = snap.CommitLimitGb > 0 ? (snap.CommitTotalGb / snap.CommitLimitGb) * 100.0 : 0.0;
        FullMemGraph.PrimaryValues = ramPct;
        FullMemGraph.SecondaryValues = commitPct;

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

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewSummary == null || ViewCpu == null || ViewMemory == null || ViewIo == null) return;

        ViewSummary.Visibility = TabSummary.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewCpu.Visibility = TabCpu.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewMemory.Visibility = TabMemory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewIo.Visibility = TabIo.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Compact up time: "3d 04h 12m", "4h 12m" or "12m".</summary>
    private static string FormatUptime(TimeSpan uptime) => uptime.TotalDays >= 1
        ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
        : uptime.Hours > 0
            ? $"{uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m";
}
