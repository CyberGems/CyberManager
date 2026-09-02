using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class SettingsWindow : Window
{
    public Action? OnSettingsChanged { get; set; }
    private bool _initializing = true;
    private readonly DispatcherTimer _saveDebounceTimer = new();

    public SettingsWindow()
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);

        _saveDebounceTimer.Interval = TimeSpan.FromMilliseconds(200);
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            App.Settings.Save();
        };

        Loaded += OnLoaded;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _initializing = true;

        // Process View Settings
        ShowIdleSwitch.IsChecked = App.Settings.ShowIdleProcess;
        GroupByAppSwitch.IsChecked = App.Settings.GroupProcesses;
        HighlightSuspendedSwitch.IsChecked = App.Settings.ShowSuspended;

        // Language
        LangComboBox.SelectedIndex = App.Settings.Language == Lang.Es ? 0 : 1;

        // Theme
        switch (App.Settings.Theme)
        {
            case AppTheme.CyberManager:
                ThemeCyberManagerRadio.IsChecked = true;
                break;
            case AppTheme.Dark:
                ThemeDarkRadio.IsChecked = true;
                break;
            case AppTheme.Light:
                ThemeLightRadio.IsChecked = true;
                break;
        }

        // Font Size
        FontSizeSlider.Value = App.Settings.RowFontSize;
        FontSizeValLbl.Text = $"{App.Settings.RowFontSize:F0}px";

        // Refresh Interval
        RefreshIntervalComboBox.SelectedIndex = App.Settings.RefreshIntervalMs switch
        {
            <= 550 => 0,  // 500 ms
            >= 1500 => 2, // 2000 ms
            _ => 1        // 800 ms
        };

        // System Settings
        StartWithWinSwitch.IsChecked = App.Settings.StartWithWindows;
        MinimizeToTraySwitch.IsChecked = App.Settings.MinimizeToTray;
        AlwaysOnTopSwitch.IsChecked = App.Settings.AlwaysOnTop;
        AutoUpdatesSwitch.IsChecked = App.Settings.AutoCheckForUpdates;

        ApplyLanguage();
        _initializing = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        App.Settings.ShowIdleProcess = ShowIdleSwitch.IsChecked == true;
        App.Settings.GroupProcesses = GroupByAppSwitch.IsChecked == true;
        App.Settings.ShowSuspended = HighlightSuspendedSwitch.IsChecked == true;
        App.Settings.StartWithWindows = StartWithWinSwitch.IsChecked == true;
        App.Settings.MinimizeToTray = MinimizeToTraySwitch.IsChecked == true;
        App.Settings.AlwaysOnTop = AlwaysOnTopSwitch.IsChecked == true;
        App.Settings.AutoCheckForUpdates = AutoUpdatesSwitch.IsChecked == true;

        // Sync Start with Windows Registry
        if (sender == StartWithWinSwitch)
        {
            StartupManager.SetAutoStart(App.Settings.StartWithWindows);
        }

        SaveAndNotify();
    }

    private void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;

        var newLang = LangComboBox.SelectedIndex == 0 ? Lang.Es : Lang.En;
        if (App.Settings.Language != newLang)
        {
            App.Settings.Language = newLang;
            Strings.Current = newLang;
            ApplyLanguage();
            SaveAndNotify();
        }
    }

    private void ThemeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        AppTheme theme = AppTheme.CyberManager;
        if (ThemeDarkRadio.IsChecked == true) theme = AppTheme.Dark;
        else if (ThemeLightRadio.IsChecked == true) theme = AppTheme.Light;

        if (App.Settings.Theme != theme)
        {
            App.Settings.Theme = theme;
            ThemeManager.Apply(theme);
            SaveAndNotify();
        }
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeValLbl != null)
        {
            FontSizeValLbl.Text = $"{e.NewValue:F0}px";
        }

        if (_initializing) return;

        App.Settings.RowFontSize = e.NewValue;
        SaveAndNotify();
    }

    private void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;

        double ms = RefreshIntervalComboBox.SelectedIndex switch
        {
            0 => 500,
            2 => 2000,
            _ => 800
        };

        App.Settings.RefreshIntervalMs = ms;
        SaveAndNotify();
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _initializing = true;

        App.Settings.ShowIdleProcess = false;
        App.Settings.GroupProcesses = true;
        App.Settings.ShowSuspended = true;
        App.Settings.RefreshIntervalMs = 800;
        App.Settings.AlwaysOnTop = false;
        App.Settings.MinimizeToTray = true;
        App.Settings.StartWithWindows = false;
        App.Settings.AutoCheckForUpdates = true;
        App.Settings.RowFontSize = 13.0;

        StartupManager.SetAutoStart(false);

        ShowIdleSwitch.IsChecked = false;
        GroupByAppSwitch.IsChecked = true;
        HighlightSuspendedSwitch.IsChecked = true;
        AlwaysOnTopSwitch.IsChecked = false;
        MinimizeToTraySwitch.IsChecked = true;
        StartWithWinSwitch.IsChecked = false;
        AutoUpdatesSwitch.IsChecked = true;
        FontSizeSlider.Value = 13.0;
        RefreshIntervalComboBox.SelectedIndex = 1;

        _initializing = false;
        SaveAndNotify();
        ShowToast(Strings.T("SettingsSaved"));
    }

    private void SaveAndNotify()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
        OnSettingsChanged?.Invoke();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        ToastText.BeginAnimation(OpacityProperty, anim);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            ToastText.BeginAnimation(OpacityProperty, fadeOut);
        };
        timer.Start();
    }

    public void ApplyLanguage()
    {
        TitleText.Text = Strings.T("SettingsTitle");
        SubtitleText.Text = Strings.T("SettingsSubtitle");

        // Section Headers
        SecProcessViewLbl.Text = Strings.T("ProcessViewSettings");
        SecGeneralLbl.Text = Strings.T("GeneralSettings");
        SecPerfLbl.Text = Strings.T("PerformanceSettings");
        SecSystemLbl.Text = Strings.T("SystemSettings");

        // Process View items
        ShowIdleTitleLbl.Text = Strings.T("ShowIdleProcessTitle");
        ShowIdleDescLbl.Text = Strings.T("ShowIdleProcessDesc");
        GroupByAppTitleLbl.Text = Strings.T("GroupByAppTitle");
        GroupByAppDescLbl.Text = Strings.T("GroupByAppDesc");
        HighlightSuspendedTitleLbl.Text = Strings.T("HighlightSuspendedTitle");
        HighlightSuspendedDescLbl.Text = Strings.T("HighlightSuspendedDesc");

        // General
        LanguageTitleLbl.Text = Strings.T("Language");
        LanguageDescLbl.Text = Strings.Current == Lang.Es ? "Idioma usado en menús, ventanas y métricas." : "Language used across menus, dialogs, and metrics.";
        ThemeTitleLbl.Text = Strings.T("Theme");
        ThemeDescLbl.Text = Strings.Current == Lang.Es ? "Elige el aspecto visual característico de CyberManager." : "Select visual accent and background style.";
        TextSizeTitleLbl.Text = Strings.T("TextSize");
        TextSizeDescLbl.Text = Strings.Current == Lang.Es ? "Ajusta el tamaño de fuente de la tabla para lectura cómoda." : "Adjust table font size for compact or comfortable viewing.";

        // Performance
        RefreshRateTitleLbl.Text = Strings.T("RefreshIntervalTitle");
        RefreshRateDescLbl.Text = Strings.T("RefreshIntervalDesc");

        // System
        StartWithWinTitleLbl.Text = Strings.T("StartWithWindowsTitle");
        StartWithWinDescLbl.Text = Strings.T("StartWithWindowsDesc");
        MinimizeToTrayTitleLbl.Text = Strings.T("MinimizeToTrayTitle");
        MinimizeToTrayDescLbl.Text = Strings.T("MinimizeToTrayDesc");
        AlwaysOnTopTitleLbl.Text = Strings.T("AlwaysOnTopTitle");
        AlwaysOnTopDescLbl.Text = Strings.T("AlwaysOnTopDesc");
        AutoUpdatesTitleLbl.Text = Strings.T("AutoCheckUpdatesTitle");
        AutoUpdatesDescLbl.Text = Strings.T("AutoCheckUpdatesDesc");
        HotkeyTitleLbl.Text = Strings.T("GlobalHotkeyTitle");
        HotkeyDescLbl.Text = Strings.T("GlobalHotkeyDesc");

        // Buttons
        ResetBtn.Content = Strings.T("ResetDefaults");
        CloseBtn.Content = Strings.T("Close");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Save();
        Close();
    }
}
