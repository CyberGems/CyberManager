using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public enum ConfirmIconType
{
    Default,
    Trash,
    Warning,
    Check,
    Info
}

public partial class ConfirmDialog : Window
{
    private static readonly PathToIconConverter IconConv = new();
    private readonly string? _confirmationKey;

    public ConfirmDialog(string title, string message, string exePath, string okText = "", string? cancelText = null, bool isDanger = false, string? confirmationKey = null)
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        _confirmationKey = confirmationKey;

        TitleLbl.Text = title;
        MessageLbl.Text = message;
        ConfigureButtons(okText, cancelText);
        if (isDanger)
        {
            OkBtn.Style = FindResource("DangerButtonStyle") as Style;
        }

        ConfigureConfirmation();

        var icon = !string.IsNullOrEmpty(exePath) ? IconConv.Convert(exePath, typeof(ImageSource), null!, null!) as ImageSource : null;
        if (icon != null)
        {
            AppIconImg.Source = icon;
            AppIconImg.Visibility = Visibility.Visible;
            TrashPath.Visibility = Visibility.Collapsed;
            AlertPath.Visibility = Visibility.Collapsed;
            CheckPath.Visibility = Visibility.Collapsed;
        }
        else if (isDanger)
        {
            AppIconImg.Visibility = Visibility.Collapsed;
            TrashPath.Visibility = Visibility.Visible;
            AlertPath.Visibility = Visibility.Collapsed;
            CheckPath.Visibility = Visibility.Collapsed;
        }
        else
        {
            AppIconImg.Visibility = Visibility.Collapsed;
            TrashPath.Visibility = Visibility.Collapsed;
            AlertPath.Visibility = Visibility.Visible;
            CheckPath.Visibility = Visibility.Collapsed;
        }
    }

    public ConfirmDialog(string title, string message, string okText = "", string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default, bool isDanger = false, string? confirmationKey = null)
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        _confirmationKey = confirmationKey;

        TitleLbl.Text = title;
        MessageLbl.Text = message;
        ConfigureButtons(okText, cancelText);
        if (isDanger)
        {
            OkBtn.Style = FindResource("DangerButtonStyle") as Style;
        }

        ConfigureConfirmation();

        AppIconImg.Visibility = Visibility.Collapsed;
        TrashPath.Visibility = Visibility.Collapsed;
        AlertPath.Visibility = Visibility.Collapsed;
        CheckPath.Visibility = Visibility.Collapsed;

        if (iconType == ConfirmIconType.Check)
        {
            CheckPath.Visibility = Visibility.Visible;
        }
        else if (iconType == ConfirmIconType.Trash || isDanger)
        {
            TrashPath.Visibility = Visibility.Visible;
        }
        else
        {
            AlertPath.Visibility = Visibility.Visible;
        }
    }

    public static bool Show(Window? owner, string title, string message, string okText = "", string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default, bool isDanger = false, string? confirmationKey = null)
    {
        if (ShouldSkipConfirmation(confirmationKey)) return true;

        var dlg = new ConfirmDialog(title, message, okText, cancelText, iconType, isDanger, confirmationKey);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    public static bool ShowProcess(Window? owner, string title, string message, string exePath, string okText = "", string? cancelText = null, bool isDanger = true, string? confirmationKey = null)
    {
        if (ShouldSkipConfirmation(confirmationKey)) return true;

        var dlg = new ConfirmDialog(title, message, exePath, okText, cancelText, isDanger, confirmationKey);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private static bool ShouldSkipConfirmation(string? confirmationKey) =>
        !string.IsNullOrWhiteSpace(confirmationKey) &&
        App.Settings.IsConfirmationSuppressed(confirmationKey);

    private void ConfigureButtons(string okText, string? cancelText)
    {
        OkTextLbl.Text = string.IsNullOrWhiteSpace(okText) ? Strings.T("Ok") : okText;
        AutomationProperties.SetName(OkBtn, OkTextLbl.Text);

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelTextLbl.Text = cancelText;
            AutomationProperties.SetName(CancelBtn, cancelText);
            CancelBtn.Visibility = Visibility.Visible;
        }
    }

    private void ConfigureConfirmation()
    {
        DoNotShowAgainCheck.Content = Strings.T("DoNotShowAgain");
        AutomationProperties.SetName(DoNotShowAgainCheck, Strings.T("DoNotShowAgain"));
        DoNotShowAgainCheck.Visibility = string.IsNullOrWhiteSpace(_confirmationKey)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null) return;

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_confirmationKey) && DoNotShowAgainCheck.IsChecked == true)
        {
            App.Settings.SetConfirmationSuppressed(_confirmationKey, suppressed: true);
            App.Settings.Save();
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
