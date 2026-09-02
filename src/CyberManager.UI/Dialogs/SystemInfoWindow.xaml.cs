using System.Windows;
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
        // Smooth glow effect when attention is requested
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
        TabSummary.Content = Strings.T("Summary");
        TabCpu.Content = Strings.T("Cpu");
        TabMemory.Content = Strings.T("Memory");
        TabIo.Content = Strings.T("IoHistory");

        CpuHistoryTitle.Text = Strings.T("CpuHistory");
        MemHistoryTitle.Text = Strings.T("MemoryHistory");
        TotalsCardTitle.Text = Strings.T("Totals");
        HandlesLbl.Text = Strings.T("Handles");
        ThreadsLbl.Text = Strings.T("Threads");
        ProcessesLbl.Text = Strings.T("Processes");

        CpuCardTitle.Text = Strings.T("Cpu");
        CpuTotalLbl.Text = Strings.T("TotalMemory");
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
        var (ramGb, ramPct) = SystemMetricsCollector.Instance.GetRamHistory();
        var commitGb = SystemMetricsCollector.Instance.GetCommitHistory();

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
        SummaryMemGraph.SecondaryValues = null;

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

        CoresVal.Text = snap.PhysicalCores.ToString();
        SocketsVal.Text = snap.Sockets.ToString();
        LogProcVal.Text = snap.LogicalProcessors.ToString();

        // 4. Full CPU Tab
        FullCpuGraph.CurrentValue = snap.CpuTotalPercent;
        FullCpuGraph.SecondaryCurrentValue = snap.CpuKernelPercent;
        FullCpuGraph.PrimaryValues = cpuTotal;
        FullCpuGraph.SecondaryValues = cpuKernel;

        FullCpuTotal.Text = $"{snap.CpuTotalPercent:F1}%";
        FullCpuKernel.Text = $"{snap.CpuKernelPercent:F1}%";
        FullCpuUser.Text = $"{snap.CpuUserPercent:F1}%";
        FullCpuModel.Text = snap.CpuModelName;
        FullCpuCores.Text = snap.PhysicalCores.ToString();
        FullCpuLogical.Text = snap.LogicalProcessors.ToString();

        // 5. Full Memory Tab
        FullMemGraph.CurrentValue = snap.MemoryLoadPercent;
        FullMemGraph.SecondaryCurrentValue = snap.CommitLimitGb > 0 ? (snap.CommitTotalGb / snap.CommitLimitGb) * 100.0 : 0.0;
        FullMemGraph.PrimaryValues = ramPct;
        FullMemGraph.SecondaryValues = null;

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
        IoPagedVal.Text = $"{snap.PagedPoolMb:F0} MB";
        IoNonPagedVal.Text = $"{snap.NonPagedPoolMb:F0} MB";
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
}
