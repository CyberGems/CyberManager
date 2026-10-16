using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Controls;

/// <summary>
/// Titlebar pill announcing an available update. It stays collapsed until a check reports a newer
/// version, then shows a down-arrow icon whose label expands on hover. Both titlebars host one and
/// both read the same <see cref="UpdateService"/> state, so a check started from any surface lights
/// up every copy, and the notice survives for as long as the update is pending instead of being
/// overwritten by the next refresh tick.
/// </summary>
public partial class UpdateAvailableButton : UserControl
{
    private static readonly Duration ExpandDuration = new(TimeSpan.FromMilliseconds(200));
    private static readonly Duration CollapseDuration = new(TimeSpan.FromMilliseconds(160));

    private string? _versionLabel;
    private bool _hovered;
    private bool _keyboardFocused;

    public static readonly DependencyProperty PillHeightProperty =
        DependencyProperty.Register(
            nameof(PillHeight),
            typeof(double),
            typeof(UpdateAvailableButton),
            new PropertyMetadata(32.0, OnPillHeightChanged));

    public UpdateAvailableButton()
    {
        InitializeComponent();
        ApplyMetrics();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Raised when the user asks to act on the available update.</summary>
    public event EventHandler? UpdateRequested;

    /// <summary>
    /// Height of the pill. A single value is enough to fit the control into either titlebar: the
    /// arrow box, the corner radius and the label size are all derived from it.
    /// </summary>
    public double PillHeight
    {
        get => (double)GetValue(PillHeightProperty);
        set => SetValue(PillHeightProperty, value);
    }

    private static void OnPillHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UpdateAvailableButton button) button.ApplyMetrics();
    }

    private void ApplyMetrics()
    {
        if (PillBorder is null) return;

        var height = Math.Max(20.0, PillHeight);
        PillBorder.Height = height;
        PillBorder.CornerRadius = new CornerRadius(height / 2);

        var arrowBox = Math.Max(18.0, height - 2);
        ArrowBox.Width = arrowBox;
        ArrowBox.Height = arrowBox;

        // The compact pill is shorter, so its label steps down to stay inside the border.
        var compact = height < 30;
        LabelText.FontSize = compact ? 11 : 12;
        LabelText.Padding = compact ? new Thickness(10, 0, 5, 0) : new Thickness(12, 0, 6, 0);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateService.UpdateAvailabilityChanged += OnUpdateAvailabilityChanged;
        // The startup check may have completed before this surface was created, so the last known
        // result is applied on load instead of waiting for the next event.
        SetUpdate(UpdateService.LastCheckResult);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        UpdateService.UpdateAvailabilityChanged -= OnUpdateAvailabilityChanged;

    private void OnUpdateAvailabilityChanged(UpdateCheckResult result) =>
        Dispatcher.BeginInvoke(() => SetUpdate(result));

    /// <summary>
    /// Shows the pill for a result carrying a newer version and hides it otherwise. Pass the
    /// service result as-is; <c>null</c> means no check has completed yet.
    /// </summary>
    public void SetUpdate(UpdateCheckResult? result)
    {
        _versionLabel = result is { IsUpdateAvailable: true } ? result.LatestVersionLabel : null;
        ApplyLabel();
    }

    /// <summary>Re-applies the localised label and tooltip after a language switch.</summary>
    public void ApplyLanguage() => ApplyLabel();

    private void ApplyLabel()
    {
        if (string.IsNullOrEmpty(_versionLabel))
        {
            Visibility = Visibility.Collapsed;
            ToolTip = null;
            ResetLabel();
            return;
        }

        LabelText.Text = Strings.T("Update");
        // The pill has no room for the version itself, so the tooltip and the accessible name carry
        // the value it stands for. Both live on the button: a UserControl is not the element a
        // screen reader or an automation probe reaches.
        var announcement = Strings.T("UpdateAvailable", _versionLabel);
        ToolTip = HotkeyToolTipContent.WithShortcut(announcement, KeyboardShortcuts.Update);
        AutomationProperties.SetName(PillButton, announcement);
        Visibility = Visibility.Visible;
    }

    /// <summary>Returns the pill to its resting state so a reappearing notice starts collapsed.</summary>
    private void ResetLabel()
    {
        LabelWrapper.BeginAnimation(WidthProperty, null);
        LabelWrapper.BeginAnimation(OpacityProperty, null);
        LabelWrapper.Width = 0;
        LabelWrapper.Opacity = 0;
        // A collapsing element does not reliably report leaving the pointer or losing focus, so the
        // interaction state is cleared here instead of lingering until the notice comes back.
        _hovered = false;
        _keyboardFocused = false;
        UpdatePillVisuals();
    }

    private void PillButton_MouseEnter(object sender, MouseEventArgs e)
    {
        _hovered = true;
        UpdatePillVisuals();

        // Padding is part of the measured size, so the target width already contains the spacing.
        LabelText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var targetWidth = Math.Ceiling(LabelText.DesiredSize.Width);

        AnimateLabel(targetWidth, 1.0, ExpandDuration);
    }

    private void PillButton_MouseLeave(object sender, MouseEventArgs e)
    {
        _hovered = false;
        UpdatePillVisuals();

        AnimateLabel(0.0, 0.0, CollapseDuration);
    }

    private void PillButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _keyboardFocused = true;
        UpdatePillVisuals();
    }

    private void PillButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _keyboardFocused = false;
        UpdatePillVisuals();
    }

    /// <summary>
    /// Paints both interaction states from one place so they cannot fight over the same two
    /// brushes. Hover adds the accent glow behind the icon; keyboard focus only outlines the pill,
    /// which is what replaces the default dotted focus rectangle (that rectangle ignores the
    /// rounded shape and looked like a stray outline inside the pill). SetResourceReference keeps
    /// the colours as live theme lookups, so a theme switch repaints them.
    /// </summary>
    private void UpdatePillVisuals()
    {
        PillBorder.SetResourceReference(Border.BackgroundProperty, _hovered ? "AccentGlowBrush" : "CardSecondaryBrush");
        PillBorder.SetResourceReference(
            Border.BorderBrushProperty,
            _hovered || _keyboardFocused ? "AccentBrush" : "BorderBrush");
    }

    private void AnimateLabel(double width, double opacity, Duration duration)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        LabelWrapper.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration) { EasingFunction = ease });
        LabelWrapper.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, duration) { EasingFunction = ease });
    }

    private void PillButton_Click(object sender, RoutedEventArgs e) =>
        UpdateRequested?.Invoke(this, EventArgs.Empty);
}
