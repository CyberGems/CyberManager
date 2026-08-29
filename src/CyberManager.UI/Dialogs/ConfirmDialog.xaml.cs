using System.Windows;
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

    public ConfirmDialog(string title, string message, string exePath, string okText = "", string? cancelText = null, bool isDanger = false)
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);

        TitleLbl.Text = title;
        MessageLbl.Text = message;
        OkBtn.Content = string.IsNullOrWhiteSpace(okText) ? Strings.T("Ok") : okText;
        if (isDanger)
        {
            OkBtn.Style = FindResource("DangerButtonStyle") as Style;
        }

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelBtn.Content = cancelText;
            CancelBtn.Visibility = Visibility.Visible;
        }

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

    public ConfirmDialog(string title, string message, string okText = "", string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default, bool isDanger = false)
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);

        TitleLbl.Text = title;
        MessageLbl.Text = message;
        OkBtn.Content = string.IsNullOrWhiteSpace(okText) ? Strings.T("Ok") : okText;
        if (isDanger)
        {
            OkBtn.Style = FindResource("DangerButtonStyle") as Style;
        }

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelBtn.Content = cancelText;
            CancelBtn.Visibility = Visibility.Visible;
        }

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

    public static bool Show(Window? owner, string title, string message, string okText = "", string? cancelText = null, ConfirmIconType iconType = ConfirmIconType.Default, bool isDanger = false)
    {
        var dlg = new ConfirmDialog(title, message, okText, cancelText, iconType, isDanger);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    public static bool ShowProcess(Window? owner, string title, string message, string exePath, string okText = "", string? cancelText = null, bool isDanger = true)
    {
        var dlg = new ConfirmDialog(title, message, exePath, okText, cancelText, isDanger);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
