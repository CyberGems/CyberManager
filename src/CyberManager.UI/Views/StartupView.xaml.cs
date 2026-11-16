using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.Core.Engine;
using CyberManager.UI.Dialogs;

namespace CyberManager.UI.Views;

public partial class StartupView : UserControl
{
    private readonly StartupAppsCollector _collector = new();
    private IReadOnlyList<StartupAppInfo> _entries = Array.Empty<StartupAppInfo>();
    private bool _hasLoaded;

    public StartupView()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    /// <summary>
    /// Re-reads the entries when the tab becomes visible and after every change. Startup data is
    /// static between installs, so there is no timer and no background polling.
    /// </summary>
    public void Activate()
    {
        _entries = _collector.Collect();
        _hasLoaded = true;
        StatusText.Text = string.Empty;
        Populate();
    }

    public void RefreshLocalization()
    {
        TitleText.Text = Strings.T("StartupTitle");
        SubtitleText.Text = Strings.T("StartupSubtitle");
        NameColumn.Header = Strings.T("StartupName");
        PublisherColumn.Header = Strings.T("StartupPublisher");
        SourceColumn.Header = Strings.T("StartupSource");
        StatusColumn.Header = Strings.T("Status");
        EmptyStateText.Text = Strings.T("StartupEmpty");
        BackupNotice.Text = Strings.T("StartupBackupNotice");
        ToggleButton.ToolTip = Strings.T("StartupSelectRow");
        AutomationProperties.SetName(StartupGrid, Strings.T("StartupTitle"));

        // Source and state labels are localized, so the rows must be rebuilt on a language change.
        if (_hasLoaded) Populate();
    }

    private void Populate()
    {
        var rows = _entries
            .Select(entry => new StartupRowViewModel(
                entry,
                string.IsNullOrWhiteSpace(entry.Publisher) ? Strings.T("Unknown") : entry.Publisher,
                Strings.T(SourceKey(entry.Source)),
                entry.IsEnabled ? Strings.T("StartupEnabled") : Strings.T("StartupDisabled")))
            .ToList();

        StartupGrid.ItemsSource = rows;
        CountText.Text = Strings.T("StartupCount", rows.Count);
        CountText.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateToggleButton();
    }

    private void StartupGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateToggleButton();

    private void UpdateToggleButton()
    {
        var row = StartupGrid.SelectedItem as StartupRowViewModel;
        ToggleButton.IsEnabled = row is not null;
        ToggleButton.Content = row is null
            ? Strings.T("StartupActionChange")
            : Strings.T(row.Entry.IsEnabled ? "StartupActionDisable" : "StartupActionEnable");
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupRowViewModel row) return;

        var entry = row.Entry;
        var enabling = !entry.IsEnabled;
        var action = Strings.T(enabling ? "StartupActionEnable" : "StartupActionDisable");
        var message = Strings.T(enabling ? "StartupEnableConfirm" : "StartupDisableConfirm", entry.Name);

        var confirmed = ConfirmDialog.Show(
            Window.GetWindow(this),
            action,
            message,
            okText: action,
            cancelText: Strings.T("Cancel"),
            iconType: ConfirmIconType.Info);

        if (!confirmed) return;

        var result = StartupAppsController.SetEnabled(entry, enabling);
        if (!result.Success)
        {
            StatusText.Text = Strings.T("StartupToggleFailed");
            return;
        }

        // Re-read instead of flipping the flag locally: the list then reflects exactly what
        // Windows will do, including entries the write could not affect.
        Activate();
        StatusText.Text = Strings.T(
            enabling ? "StartupToggleDoneEnabled" : "StartupToggleDoneDisabled",
            entry.Name);
    }

    private static string SourceKey(StartupEntrySource source) => source switch
    {
        StartupEntrySource.RegistryUser => "StartupSourceRegistryUser",
        StartupEntrySource.RegistryMachine => "StartupSourceRegistryMachine",
        StartupEntrySource.RegistryMachine32 => "StartupSourceRegistryMachine32",
        StartupEntrySource.StartupFolderUser => "StartupSourceFolderUser",
        _ => "StartupSourceFolderCommon"
    };
}

/// <summary>Display row for the startup list. Public because WPF binds by reflection.</summary>
public sealed class StartupRowViewModel
{
    public StartupRowViewModel(StartupAppInfo entry, string publisher, string source, string status)
    {
        Entry = entry;
        Name = entry.Name;
        Publisher = publisher;
        Source = source;
        Status = status;
    }

    public StartupAppInfo Entry { get; }
    public string Name { get; }
    public string Publisher { get; }
    public string Source { get; }
    public string Status { get; }
    public bool IsEnabled => Entry.IsEnabled;
}
