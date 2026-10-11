using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

/// <summary>
/// What the user chose in <see cref="CloseWindowDialog"/>.
/// </summary>
public enum CloseWindowAction
{
    /// <summary>The user dismissed the dialog (X, Esc or outside click): keep the window open.</summary>
    Cancel,

    /// <summary>Hide the window and keep CyberManager running in the notification area.</summary>
    MinimizeToTray,

    /// <summary>Shut CyberManager down completely.</summary>
    Quit
}

/// <summary>
/// Close-confirmation dialog shown when the user clicks the window X, modeled on the
/// CyberNotes "Cerrar ventana" modal: a title with a close button, the question, two
/// option cards (minimize to tray / quit) with keyboard hints, and a "remember my
/// choice" checkbox.
/// </summary>
public partial class CloseWindowDialog : Window, IModalAttentionWindow
{
    /// <summary>
    /// Key under which the remembered choice is stored in
    /// <see cref="AppSettings.SuppressedConfirmations"/>. The value encoded in the key
    /// (tray / quit) lets us answer future closes without asking again.
    /// </summary>
    public const string RememberedActionKeyPrefix = "CloseWindowChoice:";

    private DateTime _lastAttentionTime = DateTime.MinValue;

    public CloseWindowAction ChosenAction { get; private set; } = CloseWindowAction.Cancel;

    /// <summary>Whether the user ticked "Remember my choice".</summary>
    public bool RememberChoice => RememberChoiceCheck.IsChecked == true;

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    public CloseWindowDialog()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);
        ApplyLocalization();

        // Space picks "Quit" the way the CyberNotes dialog does. It must not fire while
        // the checkbox has focus, otherwise Space would only toggle the checkbox.
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => TrayOptionBtn.Focus();
    }

    public void ApplyLocalization()
    {
        Title = Strings.T("CloseWindowTitle");
        DialogTitleText.Text = Strings.T("CloseWindowTitle");
        QuestionText.Text = Strings.T("CloseWindowQuestion");

        TrayOptionTitle.Text = Strings.T("CloseOptionTrayTitle");
        TrayOptionDesc.Text = Strings.T("CloseOptionTrayDesc");
        AutomationProperties.SetName(TrayOptionBtn, Strings.T("CloseOptionTrayTitle"));

        QuitOptionTitle.Text = Strings.T("CloseOptionQuitTitle");
        QuitOptionDesc.Text = Strings.T("CloseOptionQuitDesc");
        QuitKeyHintText.Text = Strings.T("KeyHintSpace");
        AutomationProperties.SetName(QuitOptionBtn, Strings.T("CloseOptionQuitTitle"));

        RememberChoiceCheck.Content = Strings.T("RememberMyChoice");
        AutomationProperties.SetName(RememberChoiceCheck, Strings.T("RememberMyChoice"));
    }

    /// <summary>
    /// Reads back a previously remembered choice. Returns <c>null</c> when the user has
    /// never asked us to remember (or the stored value is unreadable).
    /// </summary>
    public static CloseWindowAction? ReadRememberedAction(AppSettings settings)
    {
        if (settings.IsConfirmationSuppressed(RememberedActionKeyPrefix + "tray"))
            return CloseWindowAction.MinimizeToTray;
        if (settings.IsConfirmationSuppressed(RememberedActionKeyPrefix + "quit"))
            return CloseWindowAction.Quit;
        return null;
    }

    /// <summary>Persists <paramref name="action"/> as the remembered choice for future closes.</summary>
    public static void RememberAction(AppSettings settings, CloseWindowAction action)
    {
        // Only one of the two keys may be present at a time.
        settings.SetConfirmationSuppressed(RememberedActionKeyPrefix + "tray", action == CloseWindowAction.MinimizeToTray);
        settings.SetConfirmationSuppressed(RememberedActionKeyPrefix + "quit", action == CloseWindowAction.Quit);
        settings.Save();
    }

    /// <summary>Forgets any remembered close choice so the dialog is shown again.</summary>
    public static void ForgetRememberedAction(AppSettings settings)
    {
        settings.SetConfirmationSuppressed(RememberedActionKeyPrefix + "tray", false);
        settings.SetConfirmationSuppressed(RememberedActionKeyPrefix + "quit", false);
        settings.Save();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HeaderClose_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.Key != Key.Space) return;

        // Space is the shortcut for "Quit", so it wins over the standard button activation
        // even though the tray option starts focused. Only the checkbox keeps it, so the
        // user can still toggle "Remember my choice" without leaving the dialog.
        if (Keyboard.FocusedElement is CheckBox || FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null)
            return;

        e.Handled = true;
        Accept(CloseWindowAction.Quit);
    }

    private void TrayOption_Click(object sender, RoutedEventArgs e) => Accept(CloseWindowAction.MinimizeToTray);

    private void QuitOption_Click(object sender, RoutedEventArgs e) => Accept(CloseWindowAction.Quit);

    private void HeaderClose_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = CloseWindowAction.Cancel;
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null) return;
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Accept(CloseWindowAction action)
    {
        ChosenAction = action;
        if (RememberChoice) RememberAction(App.Settings, action);
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
