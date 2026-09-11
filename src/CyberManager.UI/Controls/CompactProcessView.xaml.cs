using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Common.I18n;

namespace CyberManager.UI.Controls;

public partial class CompactProcessView : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(CompactProcessView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(CompactProcessView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty RowFontSizeProperty =
        DependencyProperty.Register(
            nameof(RowFontSize),
            typeof(double),
            typeof(CompactProcessView),
            new PropertyMetadata(12.0));

    private bool _syncingSearch;

    public CompactProcessView()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public double RowFontSize
    {
        get => (double)GetValue(RowFontSizeProperty);
        set => SetValue(RowFontSizeProperty, value);
    }

    public DataGrid ProcessGrid => CompactGrid;

    public string SearchText => SearchBox.Text;

    public bool IsSearchFocused => SearchBox.IsFocused;

    public event EventHandler? SearchChanged;
    public event RoutedEventHandler? ToggleRequested;
    public event RoutedEventHandler? EndTaskRequested;
    public event RoutedEventHandler? RefreshRequested;
    public event RoutedEventHandler? PinRequested;
    public event RoutedEventHandler? SettingsRequested;
    public event RoutedEventHandler? CloseRequested;
    public event MouseButtonEventHandler? DragRequested;

    public void ApplyLanguage()
    {
        EndTaskButton.ToolTip = Strings.T("Kill");
        RefreshButton.ToolTip = Strings.T("Refresh");
        PinButton.ToolTip = Strings.T("AlwaysOnTop");
        ModeButton.ToolTip = Strings.T("MoreDetails");
        AutomationProperties.SetName(ModeButton, Strings.T("MoreDetails"));
        SettingsButton.ToolTip = Strings.T("Settings");
        CloseButton.ToolTip = Strings.T("Close");
        SearchBox.ToolTip = Strings.T("SearchPlaceholder");
        ContextHintText.Text = Strings.T("CompactContextHint");
        EmptyStateText.Text = Strings.T("NoProcesses");
        if (CompactGrid.Columns.Count >= 4)
        {
            CompactGrid.Columns[0].Header = Strings.T("Process");
            CompactGrid.Columns[1].Header = Strings.T("Pid");
            CompactGrid.Columns[2].Header = Strings.T("Cpu");
            CompactGrid.Columns[3].Header = Strings.T("Memory");
        }
    }

    public void SetSearchText(string value)
    {
        if (string.Equals(SearchBox.Text, value, StringComparison.Ordinal)) return;

        _syncingSearch = true;
        try
        {
            SearchBox.Text = value;
            SearchBox.CaretIndex = SearchBox.Text.Length;
        }
        finally
        {
            _syncingSearch = false;
        }
    }

    public void SetStatus(string value) => StatusText.Text = value;
    public void SetStatusToolTip(string value) => StatusText.ToolTip = value;

    public void SetEmptyState(bool empty)
    {
        CompactGrid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetEndTaskEnabled(bool enabled) => EndTaskButton.IsEnabled = enabled;

    public void SetPinned(bool pinned)
    {
        PinButton.Opacity = pinned ? 1.0 : 0.65;
        PinButtonHost.Background = pinned
            ? new SolidColorBrush(Color.FromArgb(0x42, 0xE8, 0x11, 0x23))
            : Brushes.Transparent;
        PinButtonHost.BorderBrush = pinned
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23))
            : Brushes.Transparent;
        PinButtonHost.BorderThickness = pinned ? new Thickness(1) : new Thickness(0);
        PinButton.ToolTip = pinned
            ? $"{Strings.T("AlwaysOnTop")} ✓"
            : Strings.T("AlwaysOnTop");
    }

    public void RefreshItems() => CompactGrid.Items.Refresh();

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncingSearch) SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter)
        {
            if (CompactGrid.Items.Count > 0)
            {
                e.Handled = true;
                if (CompactGrid.SelectedIndex < 0) CompactGrid.SelectedIndex = 0;
                CompactGrid.Focus();
                var item = CompactGrid.SelectedItem;
                if (item != null)
                {
                    CompactGrid.ScrollIntoView(item);
                    CompactGrid.UpdateLayout();
                    if (CompactGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                    {
                        row.Focus();
                    }
                }
            }

            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Clear();
            }
            else
            {
                CompactGrid.Focus();
            }
        }
    }

    private void MoreDetailsButton_Click(object sender, RoutedEventArgs e) =>
        ToggleRequested?.Invoke(sender, e);

    private void EndTaskButton_Click(object sender, RoutedEventArgs e) =>
        EndTaskRequested?.Invoke(sender, e);

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(sender, e);

    private void PinButton_Click(object sender, RoutedEventArgs e) =>
        PinRequested?.Invoke(sender, e);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(sender, e);

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(sender, e);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (FindVisualParent<Button>(source) != null ||
            FindVisualParent<TextBox>(source) != null ||
            FindVisualParent<Border>(source)?.Name == nameof(PinButtonHost))
        {
            return;
        }

        DragRequested?.Invoke(this, e);
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
