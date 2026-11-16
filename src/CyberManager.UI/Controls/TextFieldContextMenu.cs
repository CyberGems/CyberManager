using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Controls;

/// <summary>
/// The app's own context menu for editable text fields. WPF's built-in TextBox menu is the
/// generic Windows one (unthemed, in the OS language), so every <see cref="TextBox"/> gets
/// this menu instead: Undo, Redo, Cut, Copy, Paste, Select all and Delete, with the suite icons,
/// localized labels and the same glass presentation as the other menus.
///
/// Wiring is global on purpose: a class handler attaches the menu when a text field loads, so a
/// new field anywhere in the app is covered without per-surface code, and no surface can drift to
/// a second, divergent menu.
///
/// Cost is deliberately flat: the menu is built once per field, the seven icon geometries are
/// parsed once and frozen for the process lifetime, and labels/enabled states are only recomputed
/// while the menu is actually opening. Right-clicking repeatedly allocates nothing new.
/// </summary>
public static class TextFieldContextMenu
{
    private enum TextCommand
    {
        Undo,
        Redo,
        Cut,
        Copy,
        Paste,
        SelectAll,
        Delete
    }

    private static bool _installed;

    // Frozen: Geometry.Parse is done once, and every menu item in the process reuses the same
    // immutable geometry instead of re-parsing XAML path data per field or per open.
    private static readonly Geometry UndoGeometry =
        Frozen("M 8 6 L 3 10.5 L 8 15 M 3 10.5 H 13.5 A 5 5 0 0 1 13.5 20.5");

    private static readonly Geometry RedoGeometry =
        Frozen("M 16 6 L 21 10.5 L 16 15 M 21 10.5 H 10.5 A 5 5 0 0 0 10.5 20.5");

    private static readonly Geometry CutGeometry = Frozen(
        "M 7 3.5 L 17 18.5 M 17 3.5 L 7 18.5 " +
        "M 5 17.5 A 2.4 2.4 0 1 0 5 22.3 A 2.4 2.4 0 1 0 5 17.5 Z " +
        "M 19 17.5 A 2.4 2.4 0 1 0 19 22.3 A 2.4 2.4 0 1 0 19 17.5 Z");

    private static readonly Geometry CopyGeometry =
        Frozen("M 9 9 H 20 V 20 H 9 Z M 5 15 H 4 V 4 H 15 V 5");

    private static readonly Geometry PasteGeometry = Frozen(
        "M 9 3 H 15 V 6 H 9 Z M 6 5 H 18 V 21 H 6 Z M 9 10 H 15 M 9 14 H 15");

    private static readonly Geometry SelectAllGeometry = Frozen(
        "M 4 8 V 4 H 8 M 16 4 H 20 V 8 M 20 16 V 20 H 16 M 8 20 H 4 V 16");

    private static readonly Geometry DeleteGeometry = Frozen(
        "M 4 7 H 20 M 9 7 V 4 H 15 V 7 M 6 7 L 7 21 H 17 L 18 7 M 10 11 V 17 M 14 11 V 17");

    /// <summary>
    /// Registers the class handler that covers every text field. Call once, before the main
    /// window is created. Idempotent.
    /// </summary>
    public static void InstallGlobalHandler()
    {
        if (_installed) return;
        _installed = true;

        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded),
            handledEventsToo: true);
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box) Attach(box);
    }

    private static void Attach(TextBox box)
    {
        // Never replace a menu a field declares itself.
        box.ContextMenu ??= Build(box);
    }

    private static ContextMenu Build(TextBox box)
    {
        var menu = new ContextMenu();
        if (Application.Current?.TryFindResource("ModernContextMenu") is Style menuStyle)
        {
            menu.Style = menuStyle;
        }

        menu.Items.Add(CreateItem(box, TextCommand.Undo, UndoGeometry, "Ctrl+Z"));
        menu.Items.Add(CreateItem(box, TextCommand.Redo, RedoGeometry, "Ctrl+Y"));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateItem(box, TextCommand.Cut, CutGeometry, "Ctrl+X"));
        menu.Items.Add(CreateItem(box, TextCommand.Copy, CopyGeometry, "Ctrl+C"));
        menu.Items.Add(CreateItem(box, TextCommand.Paste, PasteGeometry, "Ctrl+V"));
        menu.Items.Add(CreateItem(box, TextCommand.Delete, DeleteGeometry, null));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateItem(box, TextCommand.SelectAll, SelectAllGeometry, "Ctrl+A"));

        menu.Opened += (_, _) =>
        {
            Refresh(menu, box);

            if (PresentationSource.FromVisual(menu) is HwndSource source && source.Handle != IntPtr.Zero)
            {
                MenuGlassEffect.ApplyBlur(source.Handle, ThemeManager.IsDarkTheme);
            }
        };

        return menu;
    }

    private static MenuItem CreateItem(TextBox box, TextCommand command, Geometry icon, string? gesture)
    {
        var item = new MenuItem
        {
            Tag = command,
            InputGestureText = gesture,
            Icon = CreateIcon(icon)
        };

        if (Application.Current?.TryFindResource("ModernMenuItem") is Style itemStyle)
        {
            item.Style = itemStyle;
        }

        item.Click += (_, _) => Execute(box, command);
        return item;
    }

    private static Separator CreateSeparator()
    {
        var separator = new Separator();
        if (Application.Current?.TryFindResource("ModernMenuSeparator") is Style separatorStyle)
        {
            separator.Style = separatorStyle;
        }

        return separator;
    }

    private static Path CreateIcon(Geometry geometry)
    {
        var icon = new Path
        {
            Data = geometry,
            StrokeThickness = 1.35,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Brushes.Transparent
        };

        // Resource reference, not the resolved brush: the icon must follow theme changes.
        icon.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        return icon;
    }

    /// <summary>
    /// Labels and enabled states are resolved only on open, so a language switch is picked up by
    /// the next right click and a closed menu costs nothing.
    /// </summary>
    private static void Refresh(ContextMenu menu, TextBox box)
    {
        foreach (var raw in menu.Items)
        {
            if (raw is not MenuItem { Tag: TextCommand command } item) continue;

            item.Header = Strings.T(HeaderKey(command));
            item.InputGestureText = command == TextCommand.Delete
                ? Strings.T("DeleteKey")
                : GestureFor(command);
            item.IsEnabled = CanExecute(box, command);
        }
    }

    private static string HeaderKey(TextCommand command) => command switch
    {
        TextCommand.Undo => "TextMenuUndo",
        TextCommand.Redo => "TextMenuRedo",
        TextCommand.Cut => "TextMenuCut",
        TextCommand.Copy => "TextMenuCopy",
        TextCommand.Paste => "TextMenuPaste",
        TextCommand.SelectAll => "TextMenuSelectAll",
        _ => "TextMenuDelete"
    };

    private static string GestureFor(TextCommand command) => command switch
    {
        TextCommand.Undo => "Ctrl+Z",
        TextCommand.Redo => "Ctrl+Y",
        TextCommand.Cut => "Ctrl+X",
        TextCommand.Copy => "Ctrl+C",
        TextCommand.Paste => "Ctrl+V",
        TextCommand.SelectAll => "Ctrl+A",
        _ => string.Empty
    };

    private static bool CanExecute(TextBox box, TextCommand command)
    {
        var editable = !box.IsReadOnly;
        return command switch
        {
            TextCommand.Undo => editable && box.CanUndo,
            TextCommand.Redo => editable && box.CanRedo,
            TextCommand.Cut => editable && box.SelectionLength > 0,
            TextCommand.Copy => box.SelectionLength > 0,
            TextCommand.Paste => editable && ClipboardHasText(),
            TextCommand.Delete => editable && (box.SelectionLength > 0 || box.CaretIndex < box.Text.Length),
            TextCommand.SelectAll => box.Text.Length > 0,
            _ => false
        };
    }

    private static bool ClipboardHasText()
    {
        try
        {
            return Clipboard.ContainsText();
        }
        catch
        {
            // The clipboard can be locked by another process; treat it as unavailable so the
            // item is disabled instead of throwing while the menu opens.
            return false;
        }
    }

    private static void Execute(TextBox box, TextCommand command)
    {
        switch (command)
        {
            case TextCommand.Undo:
                if (box.CanUndo) box.Undo();
                break;
            case TextCommand.Redo:
                if (box.CanRedo) box.Redo();
                break;
            case TextCommand.Cut:
                if (!box.IsReadOnly && box.SelectionLength > 0) box.Cut();
                break;
            case TextCommand.Copy:
                if (box.SelectionLength > 0) box.Copy();
                break;
            case TextCommand.Paste:
                if (!box.IsReadOnly && ClipboardHasText()) box.Paste();
                break;
            case TextCommand.SelectAll:
                box.SelectAll();
                break;
            case TextCommand.Delete:
                Delete(box);
                break;
        }
    }

    private static void Delete(TextBox box)
    {
        if (box.IsReadOnly) return;

        if (box.SelectionLength > 0)
        {
            box.SelectedText = string.Empty;
            return;
        }

        var caret = box.CaretIndex;
        if (caret >= box.Text.Length) return;

        box.Select(caret, 1);
        box.SelectedText = string.Empty;
    }

    private static Geometry Frozen(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
