using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.UI.Controls;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class ProcessPropertiesWindow : Window
{
    private readonly ProcessInfo _process;
    private readonly ProcessMetadata _metadata;
    private DispatcherTimer? _copiedTipTimer;
    private Button? _copiedTipButton;
    private object? _copiedTipBase;

    public ProcessPropertiesWindow(ProcessInfo process)
    {
        _process = process;
        _metadata = ProcessMetadataResolver.Resolve(process);

        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as System.Windows.Media.ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);

        RefreshLocalization();
        PopulateValues();
    }

    private void RefreshLocalization()
    {
        Title = $"{Strings.T("ProcessProperties")} ᐧ {_metadata.FriendlyName}";
        TitleText.Text = Strings.T("ProcessProperties");
        FriendlyNameText.Text = _metadata.FriendlyName;

        IdentitySectionText.Text = Strings.T("Process");
        FileSectionText.Text = Strings.T("FileInformation");
        RuntimeSectionText.Text = Strings.T("Runtime");

        FriendlyNameLabel.Text = Strings.T("FriendlyName");
        ImageNameLabel.Text = Strings.T("ImageName");
        PidLabel.Text = Strings.T("Pid");
        PathLabel.Text = Strings.T("Path");
        CompanyLabel.Text = Strings.T("Company");
        ProductLabel.Text = Strings.T("Product");
        VersionLabel.Text = Strings.T("Version");
        OriginalFilenameLabel.Text = Strings.T("OriginalFilename");
        UserLabel.Text = Strings.T("User");
        StatusLabel.Text = Strings.T("Status");
        StartTimeLabel.Text = Strings.T("StartTime");
        CpuLabel.Text = Strings.T("Cpu");
        MemoryLabel.Text = Strings.T("Memory");
        ThreadsLabel.Text = Strings.T("Threads");
        PriorityLabel.Text = Strings.T("Priority");
        ParentProcessLabel.Text = Strings.T("ParentProcess");
        WindowTitleLabel.Text = Strings.T("WindowTitle");
        InstancesLabel.Text = Strings.T("Instances");

        CloseButton.ToolTip = Strings.T("Close");
        CloseFooterText.Text = Strings.T("Close");
        AutomationProperties.SetName(CloseButton, Strings.T("Close"));
        AutomationProperties.SetName(CloseFooterButton, Strings.T("Close"));

        CopyPathButton.ToolTip = Strings.T("CopyPath");
        AutomationProperties.SetName(CopyPathButton, Strings.T("CopyPath"));
        CopyInfoButton.ToolTip = HotkeyToolTipContent.WithShortcut(Strings.T("CopyProcessInfo"), KeyboardShortcuts.CopyProcessInfoWindow);
        AutomationProperties.SetName(CopyInfoButton, Strings.T("CopyProcessInfo"));
    }

    private void PopulateValues()
    {
        var unknown = Strings.T("Unknown");

        FriendlyNameValue.Text = ProcessSummaryText.ValueOrUnknown(_metadata.FriendlyName, unknown);
        ImageNameValue.Text = ProcessSummaryText.ValueOrUnknown(_process.Name, unknown);
        PidValue.Text = _process.Pid > 0 ? _process.Pid.ToString(CultureInfo.CurrentCulture) : unknown;
        PathValue.Text = ProcessSummaryText.ValueOrUnknown(_process.ExePath, unknown);
        CopyPathButton.IsEnabled = !string.IsNullOrWhiteSpace(_process.ExePath);

        CompanyValue.Text = ProcessSummaryText.ValueOrUnknown(_metadata.CompanyName, unknown);
        ProductValue.Text = ProcessSummaryText.ValueOrUnknown(_metadata.ProductName, unknown);
        VersionValue.Text = ProcessSummaryText.ValueOrUnknown(_metadata.FileVersion, unknown);
        OriginalFilenameValue.Text = ProcessSummaryText.ValueOrUnknown(_metadata.OriginalFilename, unknown);

        UserValue.Text = ProcessSummaryText.ValueOrUnknown(_process.UserName, unknown);
        StatusValue.Text = ProcessSummaryText.FormatStatus(_process.Status, unknown);
        StartTimeValue.Text = _process.StartTime == default
            ? unknown
            : _process.StartTime.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        CpuValue.Text = _process.CpuFormatted;
        MemoryValue.Text = _process.MemoryFormatted;
        ThreadsValue.Text = _process.ThreadCount.ToString(CultureInfo.CurrentCulture);
        PriorityValue.Text = _process.PriorityFormatted;
        ParentProcessValue.Text = _process.ParentPid > 0
            ? _process.ParentPid.ToString(CultureInfo.CurrentCulture)
            : unknown;
        WindowTitleValue.Text = ProcessSummaryText.ValueOrUnknown(_process.MainWindowTitle, unknown);
        InstancesValue.Text = Math.Max(1, _process.InstanceCount).ToString(CultureInfo.CurrentCulture);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _process.ExePath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Clipboard.SetText(path.Trim());
        }
        catch
        {
            // Clipboard can be transiently locked by another process; ignore.
            return;
        }

        ShowCopiedFeedback(CopyPathButton, Strings.T("CopyPath"), Strings.T("PathCopied"));
    }

    private void CopyInfoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(BuildProcessSummary());
        }
        catch
        {
            // Clipboard can be transiently locked by another process; ignore.
            return;
        }

        ShowCopiedFeedback(CopyInfoButton, Strings.T("CopyProcessInfo"), Strings.T("ProcessInfoCopied"));
    }

    private void ShowCopiedFeedback(Button button, object baseTooltip, string copiedTooltip)
    {
        RestoreCopiedTooltip();
        _copiedTipButton = button;
        _copiedTipBase = baseTooltip;
        button.ToolTip = copiedTooltip;

        _copiedTipTimer ??= CreateCopiedTipTimer();
        _copiedTipTimer.Start();
    }

    private DispatcherTimer CreateCopiedTipTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        timer.Tick += (_, _) => RestoreCopiedTooltip();
        return timer;
    }

    private void RestoreCopiedTooltip()
    {
        _copiedTipTimer?.Stop();
        if (_copiedTipButton != null && _copiedTipBase != null)
            _copiedTipButton.ToolTip = _copiedTipBase;
        _copiedTipButton = null;
        _copiedTipBase = null;
    }

    /// <summary>Builds a plain-text summary of the values currently shown in the window.</summary>
    private string BuildProcessSummary() => ProcessSummaryText.Build(_process);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+C mirrors the footer copy button; the window has no editable text fields,
        // so the gesture has no other meaning here.
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            CopyInfoButton_Click(sender, e);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            e.OriginalSource is DependencyObject source && FindVisualParent<Button>(source) != null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
