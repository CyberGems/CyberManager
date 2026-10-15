using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberManager.UI.Services;

/// <summary>Smooths only vertical scrollbar line commands on the process grids.</summary>
public static class ProcessListScrollBehavior
{
    public static readonly DependencyProperty SmoothChevronsProperty = DependencyProperty.RegisterAttached(
        "SmoothChevrons", typeof(bool), typeof(ProcessListScrollBehavior),
        new PropertyMetadata(false, OnSmoothChevronsChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(ChevronScrollState), typeof(ProcessListScrollBehavior));

    public static bool GetSmoothChevrons(DependencyObject element) => (bool)element.GetValue(SmoothChevronsProperty);
    public static void SetSmoothChevrons(DependencyObject element, bool value) => element.SetValue(SmoothChevronsProperty, value);

    private static void OnSmoothChevronsChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not DataGrid grid) return;
        if (grid.GetValue(StateProperty) is ChevronScrollState previous) previous.Detach();
        grid.SetValue(StateProperty, (bool)args.NewValue ? new ChevronScrollState(grid) : null);
    }

    private sealed class ChevronScrollState
    {
        // Match WPF's pixel-based LineUp/LineDown distance, keeping the existing repeat speed.
        private const double LineDistance = 16;
        private const double DurationMilliseconds = 80;
        private readonly DataGrid _grid;
        private ScrollViewer? _viewer;
        private IScrollInfo? _scrollInfo;
        private double _startOffset;
        private double _targetOffset;
        private double _lastOffset;
        private int _direction;
        private long _startedAt;
        private TimeSpan? _lastRenderingTime;

        internal ChevronScrollState(DataGrid grid)
        {
            _grid = grid;
            CommandManager.AddPreviewExecutedHandler(grid, OnPreviewExecuted);
            grid.PreviewMouseWheel += OnMouseWheel;
            grid.PreviewMouseDown += OnMouseDown;
            grid.PreviewKeyDown += OnKeyDown;
            grid.Unloaded += OnUnloaded;
            grid.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted), true);
        }

        internal void Detach()
        {
            Stop();
            CommandManager.RemovePreviewExecutedHandler(_grid, OnPreviewExecuted);
            _grid.PreviewMouseWheel -= OnMouseWheel;
            _grid.PreviewMouseDown -= OnMouseDown;
            _grid.PreviewKeyDown -= OnKeyDown;
            _grid.Unloaded -= OnUnloaded;
            _grid.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted));
        }

        private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs args)
        {
            var bar = FindAncestor<ScrollBar>(args.OriginalSource as DependencyObject);
            if (bar?.Orientation != Orientation.Vertical || !ReferenceEquals(FindAncestor<DataGrid>(bar), _grid)) return;

            var direction = args.Command == ScrollBar.LineDownCommand ? 1
                : args.Command == ScrollBar.LineUpCommand ? -1 : 0;
            if (direction == 0)
            {
                // Track paging and thumb commands must take over immediately.
                Stop();
                return;
            }

            var viewer = FindAncestor<ScrollViewer>(bar);
            if (viewer is null || !viewer.CanContentScroll ||
                VirtualizingPanel.GetScrollUnit(_grid) != ScrollUnit.Pixel || !SystemParameters.ClientAreaAnimation) return;

            var continuing = ReferenceEquals(_viewer, viewer);
            var scrollInfo = continuing ? _scrollInfo : FindDescendant<DataGridRowsPresenter>(_grid);
            if (scrollInfo is null || !ReferenceEquals(scrollInfo.ScrollOwner, viewer)) return;
            var from = continuing ? _lastOffset : scrollInfo.VerticalOffset;
            var target = continuing && direction == _direction ? _targetOffset : from;
            _targetOffset = Math.Clamp(target + direction * LineDistance, 0, viewer.ScrollableHeight);
            _startOffset = _lastOffset = from;
            _direction = direction;
            _startedAt = Stopwatch.GetTimestamp();
            if (!continuing)
            {
                Stop();
                _viewer = viewer;
                _scrollInfo = scrollInfo;
                CompositionTarget.Rendering += OnRendering;
            }

            args.Handled = true;
        }

        private void OnRendering(object? sender, EventArgs args)
        {
            if (_viewer is not { } viewer || args is not RenderingEventArgs rendering ||
                rendering.RenderingTime == _lastRenderingTime) return;
            _lastRenderingTime = rendering.RenderingTime;

            var progress = Math.Clamp(Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds / DurationMilliseconds, 0, 1);
            var eased = 1 - (1 - progress) * (1 - progress);
            _lastOffset = Math.Clamp(_startOffset + (_targetOffset - _startOffset) * eased, 0, viewer.ScrollableHeight);
            // Apply to the existing pixel virtualizer, just as WPF's wheel handler does.
            // Queuing ScrollToVerticalOffset here would let a stale animation command run
            // after native wheel input, overwriting its position even after cancellation.
            _scrollInfo!.SetVerticalOffset(_lastOffset);
            if (progress >= 1) Stop();
        }

        private void Stop()
        {
            if (_viewer is null) return;
            CompositionTarget.Rendering -= OnRendering;
            _viewer = null;
            _scrollInfo = null;
            _lastRenderingTime = null;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs args) => Stop();
        private void OnMouseDown(object sender, MouseButtonEventArgs args) => Stop();
        private void OnKeyDown(object sender, KeyEventArgs args) => Stop();
        private void OnDragStarted(object sender, DragStartedEventArgs args) => Stop();
        private void OnUnloaded(object sender, RoutedEventArgs args) => Stop();
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T ancestor) return ancestor;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject element) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } descendant) return descendant;
        }

        return null;
    }
}
