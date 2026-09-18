using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class ProcessPropertiesWindow : Window
{
    private readonly ProcessInfo _process;
    private readonly ProcessMetadata _metadata;

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
    }

    private void PopulateValues()
    {
        var unknown = Strings.T("Unknown");

        FriendlyNameValue.Text = ValueOrUnknown(_metadata.FriendlyName, unknown);
        ImageNameValue.Text = ValueOrUnknown(_process.Name, unknown);
        PidValue.Text = _process.Pid > 0 ? _process.Pid.ToString(CultureInfo.CurrentCulture) : unknown;
        PathValue.Text = ValueOrUnknown(_process.ExePath, unknown);

        CompanyValue.Text = ValueOrUnknown(_metadata.CompanyName, unknown);
        ProductValue.Text = ValueOrUnknown(_metadata.ProductName, unknown);
        VersionValue.Text = ValueOrUnknown(_metadata.FileVersion, unknown);
        OriginalFilenameValue.Text = ValueOrUnknown(_metadata.OriginalFilename, unknown);

        UserValue.Text = ValueOrUnknown(_process.UserName, unknown);
        StatusValue.Text = FormatStatus(_process.Status, unknown);
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
        WindowTitleValue.Text = ValueOrUnknown(_process.MainWindowTitle, unknown);
        InstancesValue.Text = Math.Max(1, _process.InstanceCount).ToString(CultureInfo.CurrentCulture);
    }

    private static string ValueOrUnknown(string? value, string unknown) =>
        string.IsNullOrWhiteSpace(value) ? unknown : value.Trim();

    private static string FormatStatus(string? status, string unknown)
    {
        if (string.IsNullOrWhiteSpace(status)) return unknown;

        return status switch
        {
            "Running" => Strings.T("Running"),
            "Suspended" => Strings.T("Suspended"),
            _ => status
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
