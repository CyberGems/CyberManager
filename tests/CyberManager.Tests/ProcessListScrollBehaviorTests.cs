using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class ProcessListScrollBehaviorTests
{
    [Theory]
    [InlineData(26)]
    [InlineData(30)]
    public Task ChevronMotionKeepsPixelVirtualizationAndSelection(double rowHeight) => OnDispatcher(async () =>
    {
        using var fixture = new ScrollFixture(rowHeight);
        await Task.Delay(150);
        var offsets = new List<double>();
        var collectionChanges = 0;
        fixture.Viewer.ScrollChanged += (_, args) =>
        {
            if (args.VerticalChange != 0) offsets.Add(args.VerticalOffset);
        };
        ((INotifyCollectionChanged)fixture.Grid.Items).CollectionChanged += (_, _) => collectionChanges++;
        var selected = fixture.Grid.Items[0];
        fixture.Grid.SelectedItem = selected;

        ScrollBar.LineDownCommand.Execute(null, fixture.Bar);
        await Task.Delay(200);

        Assert.Equal(16, fixture.Viewer.VerticalOffset, 2);
        if (SystemParameters.ClientAreaAnimation)
            Assert.Contains(offsets, offset => offset > 0 && offset < 16);
        Assert.True(offsets.SequenceEqual(offsets.OrderBy(offset => offset)));
        Assert.Same(selected, fixture.Grid.SelectedItem);
        Assert.Equal(0, collectionChanges);
        Assert.Equal(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(fixture.Grid));
        Assert.True(fixture.Viewer.CanContentScroll);
        Assert.InRange(Descendants<DataGridRow>(fixture.Grid).Count(), 1, 50);

        ScrollBar.LineUpCommand.Execute(null, fixture.Bar);
        await Task.Delay(200);
        Assert.Equal(0, fixture.Viewer.VerticalOffset, 2);
    });

    [Fact]
    public Task RepeatedChevronCommandsAccumulateWithoutBackwardJumps() => OnDispatcher(async () =>
    {
        using var fixture = new ScrollFixture(30);
        await Task.Delay(150);
        var offsets = new List<double>();
        fixture.Viewer.ScrollChanged += (_, args) =>
        {
            if (args.VerticalChange != 0) offsets.Add(args.VerticalOffset);
        };

        for (var repeat = 0; repeat < 6; repeat++)
        {
            ScrollBar.LineDownCommand.Execute(null, fixture.Bar);
            await Task.Delay(33);
        }
        await Task.Delay(200);

        Assert.Equal(96, fixture.Viewer.VerticalOffset, 2);
        Assert.True(offsets.SequenceEqual(offsets.OrderBy(offset => offset)));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public Task MouseWheelTakesOverWithTheSameResultAsNativeScrolling(int elapsedMilliseconds) => OnDispatcher(async () =>
    {
        using var native = new ScrollFixture(30, smooth: false);
        using var smooth = new ScrollFixture(30);
        await Task.Delay(150);

        ScrollBar.LineDownCommand.Execute(null, smooth.Bar);
        if (elapsedMilliseconds > 0) await Task.Delay(elapsedMilliseconds);
        var beforeWheel = RaiseWheel(smooth);
        native.Viewer.ScrollToVerticalOffset(beforeWheel);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        RaiseWheel(native);
        await Task.Delay(200);

        Assert.True(native.Viewer.VerticalOffset > 0);
        Assert.Equal(native.Viewer.VerticalOffset, smooth.Viewer.VerticalOffset, 2);
    });

    [Fact]
    public Task ThumbOffsetCommandCancelsChevronMotionAndRemainsPrecise() => OnDispatcher(async () =>
    {
        using var fixture = new ScrollFixture(30);
        using var native = new ScrollFixture(30, smooth: false);
        await Task.Delay(150);
        ScrollBar.LineDownCommand.Execute(null, fixture.Bar);
        ScrollBar.ScrollToVerticalOffsetCommand.Execute(300.5, fixture.Bar);
        ScrollBar.ScrollToVerticalOffsetCommand.Execute(300.5, native.Bar);
        await Task.Delay(200);

        Assert.Equal(300.5, fixture.Viewer.VerticalOffset, 2);
        ScrollBar.LineUpCommand.Execute(null, fixture.Bar);
        ScrollBar.LineUpCommand.Execute(null, native.Bar);
        await Task.Delay(200);
        // At 125% DPI, layout rounding makes the panel's offset differ slightly from the
        // thumb's requested offset. Compare the next line step to WPF's native result.
        Assert.Equal(native.Viewer.VerticalOffset, fixture.Viewer.VerticalOffset, 2);
    });

    private static double RaiseWheel(ScrollFixture fixture)
    {
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent
        };
        fixture.Grid.RaiseEvent(preview);
        Assert.False(preview.Handled);
        var beforeWheel = Descendants<DataGridRowsPresenter>(fixture.Grid).First().VerticalOffset;
        fixture.Viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.MouseWheelEvent
        });
        return beforeWheel;
    }

    private sealed class ScrollFixture : IDisposable
    {
        private readonly Window _window;
        internal DataGrid Grid { get; }
        internal ScrollViewer Viewer { get; }
        internal ScrollBar Bar { get; }

        internal ScrollFixture(double rowHeight, bool smooth = true)
        {
            Grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                RowHeight = rowHeight,
                EnableRowVirtualization = true,
                ItemsSource = Enumerable.Range(0, 1000).Select(index => $"Process {index:D4}").ToList()
            };
            Grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding(), Width = 300 });
            VirtualizingPanel.SetIsVirtualizing(Grid, true);
            VirtualizingPanel.SetVirtualizationMode(Grid, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(Grid, ScrollUnit.Pixel);
            ScrollViewer.SetCanContentScroll(Grid, true);
            ProcessListScrollBehavior.SetSmoothChevrons(Grid, smooth);
            _window = new Window
            {
                Content = Grid,
                Width = 500,
                Height = 320,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                ShowActivated = false,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            _window.Show();
            _window.UpdateLayout();
            Viewer = Descendants<ScrollViewer>(Grid).First();
            Bar = Descendants<ScrollBar>(Viewer).First(bar => bar.Orientation == Orientation.Vertical);
        }

        public void Dispose() => _window.Close();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static Task OnDispatcher(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
