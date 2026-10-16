using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.Core.Engine;
using CyberManager.UI.Services;
using Microsoft.Win32;

namespace CyberManager.UI.Dialogs;

public partial class NewTaskDialog : Window, IModalAttentionWindow
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private bool _launching;
    private DateTime _lastAttentionTime;

    public NewTaskDialog(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);
        Width = Math.Min(640, Math.Max(320, SystemParameters.WorkArea.Width - 32));
        MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 32);
        ApplyLocalization();
        RefreshHistory();
        Loaded += (_, _) => CommandBox.Focus();
    }

    public void TriggerAttention() => ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);

    private void ApplyLocalization()
    {
        Title = Heading.Text = Strings.T("RunNewTask");
        Description.Text = Strings.T("NewTaskDescription");
        CommandLabel.Content = Strings.T("NewTaskCommand");
        Placeholder.Text = Strings.T("NewTaskPlaceholder");
        CommandHint.Text = Strings.T("NewTaskCommandHint");
        AdminLabel.Text = Strings.T("NewTaskAdministrator");
        AdminHint.Text = Strings.T("NewTaskAdminHint");
        BrowseButton.Content = Strings.T("NewTaskBrowse");
        HistoryHeading.Text = Strings.T("NewTaskHistory");
        HistoryHint.Text = Strings.T("NewTaskHistoryHint", AppSettings.MaxRecentTasks);
        HistoryEmpty.Text = Strings.T("NewTaskHistoryEmpty");
        ClearHistoryButton.Content = Strings.T("ClearTaskHistory");
        Resources["RemoveHistoryLabel"] = Strings.T("RemoveTaskHistory");
        CancelLabel.Text = Strings.T("Cancel");
        RunLabel.Text = Strings.T("NewTaskRun");
        AutomationProperties.SetName(CommandBox, Strings.T("NewTaskCommand"));
        AutomationProperties.SetName(AdminCheck, Strings.T("NewTaskAdministrator"));
        AutomationProperties.SetName(HistoryList, Strings.T("NewTaskHistory"));
        AutomationProperties.SetName(HeaderClose, Strings.T("Close"));
    }

    private void RefreshHistory()
    {
        HistoryList.ItemsSource = _settings.RecentTasks.ToArray();
        var hasHistory = HistoryList.Items.Count > 0;
        HistoryList.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmpty.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
        ClearHistoryButton.IsEnabled = hasHistory;
    }

    private void CommandBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (RunButton == null) return;
        RunButton.IsEnabled = !_launching && !string.IsNullOrWhiteSpace(CommandBox.Text);
        Placeholder.Visibility = CommandBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        if (HistoryList.SelectedItem is string selected && selected != CommandBox.Text) HistoryList.SelectedItem = null;
    }

    private void CommandBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || Keyboard.Modifiers != ModifierKeys.None || HistoryList.Items.Count == 0) return;
        HistoryList.SelectedIndex = 0;
        HistoryList.UpdateLayout();
        if (HistoryList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem first) first.Focus();
        e.Handled = true;
    }

    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is not string command) return;
        CommandBox.Text = command;
        CommandBox.CaretIndex = command.Length;
    }

    private void RemoveHistory_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not string command) return;
        e.Handled = true;
        _settings.RemoveTask(command);
        _saveSettings();
        RefreshHistory();
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClearTaskHistory();
        _saveSettings();
        RefreshHistory();
    }

    private void HistoryCommand_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not TextBlock text) return;
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentCulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
            Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        if (formatted.WidthIncludingTrailingWhitespace <= text.ActualWidth + 0.5) e.Handled = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = Strings.T("NewTaskBrowseTitle"), Filter = Strings.T("NewTaskFileFilter"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            CheckFileExists = true, Multiselect = false, DereferenceLinks = false
        };
        if (picker.ShowDialog(this) != true) return;
        CommandBox.Text = $"\"{picker.FileName}\"";
        CommandBox.Focus();
        CommandBox.CaretIndex = CommandBox.Text.Length;
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var command = CommandBox.Text.Trim();
        if (_launching || command.Length == 0) return;
        var asAdministrator = AdminCheck.IsChecked == true;
        SetBusy(true);
        TaskLaunchResult result;
        try { result = await Task.Run(() => TaskLauncher.Launch(command, asAdministrator)); }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Task launch failed: {0}", ex.GetType().Name);
            result = new(TaskLaunchStatus.Failed, ex.Message);
        }
        SetBusy(false);
        if (result.Status == TaskLaunchStatus.Started)
        {
            _settings.RecordTask(command);
            _saveSettings();
            DialogResult = true;
            return;
        }
        ErrorText.Text = result.Status switch
        {
            TaskLaunchStatus.Cancelled => Strings.T("NewTaskCancelled"),
            TaskLaunchStatus.InvalidCommand => Strings.T("NewTaskInvalidCommand"),
            _ => Strings.T("NewTaskFailed", result.ErrorMessage)
        };
        ErrorPanel.Visibility = Visibility.Visible;
        CommandBox.Focus();
        CommandBox.SelectAll();
    }

    private void SetBusy(bool busy)
    {
        _launching = busy;
        CommandBox.IsEnabled = BrowseButton.IsEnabled = AdminCheck.IsEnabled = HistoryList.IsEnabled =
            CancelButton.IsEnabled = HeaderClose.IsEnabled = !busy;
        ClearHistoryButton.IsEnabled = !busy && HistoryList.Items.Count > 0;
        RunButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(CommandBox.Text);
        RunLabel.Text = Strings.T(busy ? "NewTaskOpening" : "NewTaskRun");
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (!_launching) Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => e.Cancel = _launching;
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!_launching) Close(); }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is ButtonBase) return;
            source = VisualTreeHelper.GetParent(source);
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
