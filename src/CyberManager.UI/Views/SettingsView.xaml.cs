using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI.Views;

public partial class SettingsView : UserControl
{
    private const string DefaultGlobalHotkey = "Alt+Shift+M";

    public event Action? SettingsChanged;
    public event Action? DefaultsReset;
    public event Func<string, bool>? HotkeyChangeRequested;
    public event RoutedEventHandler? AboutRequested;

    private bool _initializing = true;
    private bool _syncingStartup;
    private bool _capturingHotkey;
    private ModifierKeys _heldHotkeyModifiers;
    private string _committedHotkey = DefaultGlobalHotkey;
    private readonly DispatcherTimer _saveDebounceTimer = new();

    public SettingsView()
    {
        InitializeComponent();

        _saveDebounceTimer.Interval = TimeSpan.FromMilliseconds(200);
        _saveDebounceTimer.Tick += async (_, _) =>
        {
            _saveDebounceTimer.Stop();
            await App.Settings.SaveAsync();
        };

        Loaded += (_, _) => LoadCurrentSettings();
    }

    public void Activate()
    {
        LoadCurrentSettings();
    }

    public void Deactivate()
    {
        _saveDebounceTimer.Stop();
        App.Settings.Save();
    }

    public void LoadCurrentSettings()
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


        // Refresh Interval
        RefreshIntervalComboBox.SelectedIndex = App.Settings.RefreshIntervalMs switch
        {
            <= 550 => 0,  // 500 ms
            >= 1500 => 2, // 2000 ms
            _ => 1        // 800 ms
        };

        // System Settings
        StartWithWinSwitch.IsChecked = App.Settings.StartWithWindows;
        MinimizeToTrayOnMinimizeSwitch.IsChecked = App.Settings.MinimizeToTrayOnMinimize;
        MinimizeToTrayOnCloseSwitch.IsChecked = App.Settings.MinimizeToTrayOnClose;
        AlwaysOnTopSwitch.IsChecked = App.Settings.AlwaysOnTop;
        AutoUpdatesSwitch.IsChecked = App.Settings.AutoCheckForUpdates;
        _committedHotkey = App.Settings.GlobalHotkey;
        HotkeyBox.Text = FormatStoredHotkey(_committedHotkey);

        RefreshLocalization();
        _initializing = false;
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        App.Settings.ShowIdleProcess = ShowIdleSwitch.IsChecked == true;
        App.Settings.GroupProcesses = GroupByAppSwitch.IsChecked == true;
        App.Settings.ShowSuspended = HighlightSuspendedSwitch.IsChecked == true;
        App.Settings.StartWithWindows = StartWithWinSwitch.IsChecked == true;
        App.Settings.MinimizeToTrayOnMinimize = MinimizeToTrayOnMinimizeSwitch.IsChecked == true;
        App.Settings.MinimizeToTrayOnClose = MinimizeToTrayOnCloseSwitch.IsChecked == true;
        App.Settings.AlwaysOnTop = AlwaysOnTopSwitch.IsChecked == true;
        App.Settings.AutoCheckForUpdates = AutoUpdatesSwitch.IsChecked == true;

        // Sync Start with Windows Registry
        if (sender == StartWithWinSwitch)
        {
            if (!_syncingStartup &&
                !StartupManager.SetAutoStart(App.Settings.StartWithWindows, requestElevation: true))
            {
                App.Settings.StartWithWindows = StartupManager.IsAutoStartEnabled();
                _syncingStartup = true;
                try
                {
                    StartWithWinSwitch.IsChecked = App.Settings.StartWithWindows;
                }
                finally
                {
                    _syncingStartup = false;
                }
                ShowToast(Strings.T("AutoStartFailed"));
            }
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
            RefreshLocalization();
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
        var previousHotkey = App.Settings.GlobalHotkey;
        App.Settings.ResetToDefaults();
        Strings.Current = App.Settings.Language;
        ThemeManager.Apply(App.Settings.Theme);

        if (HotkeyChangeRequested?.Invoke(App.Settings.GlobalHotkey) == false)
        {
            App.Settings.GlobalHotkey = previousHotkey;
            ShowToast(Strings.T("GlobalHotkeyUnavailable"));
        }

        StartupManager.SetAutoStart(App.Settings.StartWithWindows, requestElevation: true);
        LoadCurrentSettings();
        DefaultsReset?.Invoke();
        SaveAndNotify();
        ShowToast(Strings.T("SettingsSaved"));
    }

    private void SaveAndNotify()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
        SettingsChanged?.Invoke();
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

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _capturingHotkey = true;
        _heldHotkeyModifiers = ModifierKeys.None;
        HotkeyBox.Text = Strings.T("GlobalHotkeyCaptureHint");
        HotkeyBox.SelectAll();
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_capturingHotkey) return;

        _capturingHotkey = false;
        _heldHotkeyModifiers = ModifierKeys.None;
        HotkeyBox.Text = FormatStoredHotkey(_committedHotkey);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey) return;

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        var modifier = GlobalHotkeyService.GetModifier(key);
        if (modifier != ModifierKeys.None)
        {
            _heldHotkeyModifiers |= modifier;
            UpdateHotkeyPreview();
            return;
        }

        if (key == Key.None) return;

        var modifiers = _heldHotkeyModifiers | Keyboard.Modifiers;
        var candidate = GlobalHotkeyService.FormatHotkey(modifiers, key);
        HotkeyBox.Text = candidate;
        if (modifiers == ModifierKeys.None) return;

        if (TryApplyHotkey(candidate))
        {
            Keyboard.ClearFocus();
        }
        else
        {
            Keyboard.ClearFocus();
        }
    }

    private void HotkeyBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey) return;

        var modifier = GlobalHotkeyService.GetModifier(e.Key);
        if (modifier == ModifierKeys.None) return;

        _heldHotkeyModifiers &= ~modifier;
        UpdateHotkeyPreview();
        e.Handled = true;
    }

    private void HotkeyResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (TryApplyHotkey(DefaultGlobalHotkey))
        {
            ShowToast(Strings.T("SettingsSaved"));
        }
    }

    private void HotkeyClearBtn_Click(object sender, RoutedEventArgs e)
    {
        if (TryApplyHotkey(""))
        {
            ShowToast(Strings.T("SettingsSaved"));
        }
    }

    private void FooterAboutBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        AboutRequested?.Invoke(this, new RoutedEventArgs());
    }

    private void FooterAboutBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        FooterVersionText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        FooterCopyrightText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
    }

    private void FooterAboutBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        FooterVersionText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        FooterCopyrightText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
    }

    private bool TryApplyHotkey(string hotkey)
    {
        if (HotkeyChangeRequested?.Invoke(hotkey) == false)
        {
            HotkeyBox.Text = FormatStoredHotkey(_committedHotkey);
            ShowToast(Strings.T("GlobalHotkeyUnavailable"));
            return false;
        }

        App.Settings.GlobalHotkey = hotkey;
        _committedHotkey = hotkey;
        HotkeyBox.Text = FormatStoredHotkey(_committedHotkey);
        SaveAndNotify();
        return true;
    }

    private void UpdateHotkeyPreview()
    {
        var modifiers = _heldHotkeyModifiers | Keyboard.Modifiers;
        HotkeyBox.Text = modifiers == ModifierKeys.None
            ? Strings.T("GlobalHotkeyCaptureHint")
            : GlobalHotkeyService.FormatHotkey(modifiers, Key.None) + " + ...";
    }

    private static string FormatStoredHotkey(string hotkey) =>
        string.Join(
            " + ",
            hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public void RefreshLocalization()
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

        // Performance
        RefreshRateTitleLbl.Text = Strings.T("RefreshIntervalTitle");
        RefreshRateDescLbl.Text = Strings.T("RefreshIntervalDesc");

        // System
        StartWithWinTitleLbl.Text = Strings.T("StartWithWindowsTitle");
        StartWithWinDescLbl.Text = Strings.T("StartWithWindowsDesc");
        MinimizeToTrayOnMinimizeTitleLbl.Text = Strings.T("MinimizeToTrayOnMinimizeTitle");
        MinimizeToTrayOnMinimizeDescLbl.Text = Strings.T("MinimizeToTrayOnMinimizeDesc");
        MinimizeToTrayOnCloseTitleLbl.Text = Strings.T("MinimizeToTrayOnCloseTitle");
        MinimizeToTrayOnCloseDescLbl.Text = Strings.T("MinimizeToTrayOnCloseDesc");
        AlwaysOnTopTitleLbl.Text = Strings.T("AlwaysOnTopTitle");
        AlwaysOnTopDescLbl.Text = Strings.T("AlwaysOnTopDesc");
        AutoUpdatesTitleLbl.Text = Strings.T("AutoCheckUpdatesTitle");
        AutoUpdatesDescLbl.Text = Strings.T("AutoCheckUpdatesDesc");
        HotkeyTitleLbl.Text = Strings.T("GlobalHotkeyTitle");
        HotkeyDescLbl.Text = Strings.T("GlobalHotkeyDesc");
        HotkeyResetBtn.ToolTip = Strings.T("GlobalHotkeyReset");
        HotkeyClearBtn.ToolTip = Strings.T("GlobalHotkeyClear");
        HotkeyBox.ToolTip = Strings.T("GlobalHotkeyCaptureHint");
        AutomationProperties.SetName(HotkeyBox, Strings.T("GlobalHotkeyTitle"));
        AutomationProperties.SetName(HotkeyClearBtn, Strings.T("GlobalHotkeyClear"));
        AutomationProperties.SetName(HotkeyResetBtn, Strings.T("GlobalHotkeyReset"));
        FooterVersionText.Text = $"CyberManager {UpdateService.GetCurrentVersionLabel()}";
        FooterCopyrightText.Text = Strings.T("Copyright");
        FooterAboutBorder.ToolTip = Strings.T("AboutCyberManager");
        AutomationProperties.SetName(FooterAboutBorder, Strings.T("AboutCyberManager"));

        if (RefreshIntervalComboBox.Items.Count >= 3)
        {
            ((ComboBoxItem)RefreshIntervalComboBox.Items[0]).Content = Strings.T("RefreshFast");
            ((ComboBoxItem)RefreshIntervalComboBox.Items[1]).Content = Strings.T("RefreshNormal");
            ((ComboBoxItem)RefreshIntervalComboBox.Items[2]).Content = Strings.T("RefreshSlow");
        }

        // Buttons
        ResetBtn.Content = Strings.T("RestoreFactorySettings");
    }
}
