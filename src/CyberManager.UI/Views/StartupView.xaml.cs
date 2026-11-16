using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.Core.Engine;

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
    /// Re-reads the entries when the tab becomes visible. The data changes only when the user
    /// installs or removes software, so refreshing on activation keeps it current without a timer.
    /// </summary>
    public void Activate()
    {
        _entries = _collector.Collect();
        _hasLoaded = true;
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
        ReadOnlyNotice.Text = Strings.T("StartupReadOnlyNotice");
        AutomationProperties.SetName(StartupGrid, Strings.T("StartupTitle"));

        // Source and state labels are localized, so the rows must be rebuilt on a language change.
        if (_hasLoaded) Populate();
    }

    private void Populate()
    {
        var rows = _entries
            .Select(entry => new StartupRowViewModel(
                entry.Name,
                string.IsNullOrWhiteSpace(entry.Publisher) ? Strings.T("Unknown") : entry.Publisher,
                Strings.T(SourceKey(entry.Source)),
                entry.IsEnabled ? Strings.T("StartupEnabled") : Strings.T("StartupDisabled"),
                entry.IsEnabled))
            .ToList();

        StartupGrid.ItemsSource = rows;
        CountText.Text = Strings.T("StartupCount", rows.Count);
        CountText.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SourceKey(StartupEntrySource source) => source switch
    {
        StartupEntrySource.RegistryUser => "StartupSourceRegistryUser",
        StartupEntrySource.RegistryMachine => "StartupSourceRegistryMachine",
        StartupEntrySource.RegistryUser32 => "StartupSourceRegistryUser32",
        StartupEntrySource.RegistryMachine32 => "StartupSourceRegistryMachine32",
        StartupEntrySource.StartupFolderUser => "StartupSourceFolderUser",
        _ => "StartupSourceFolderCommon"
    };
}

/// <summary>Display row for the startup list. Public because WPF binds by reflection.</summary>
public sealed class StartupRowViewModel
{
    public StartupRowViewModel(string name, string publisher, string source, string status, bool isEnabled)
    {
        Name = name;
        Publisher = publisher;
        Source = source;
        Status = status;
        IsEnabled = isEnabled;
    }

    public string Name { get; }
    public string Publisher { get; }
    public string Source { get; }
    public string Status { get; }
    public bool IsEnabled { get; }
}
