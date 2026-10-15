using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.UI.Controls;
using Xunit;

namespace CyberManager.Tests;

public class AnimatedHistoryGraphTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AnimationMovesRetainedDrawingWithoutRebuildingGeometry(bool large) => OnDispatcher(async () =>
    {
        using var fixture = new GraphFixture(large);
        var probe = fixture.Probe;
        probe.Update(new float[] { 10, 20, 30, 40 }, new object());
        await FlushRender();
        Assert.False(probe.Graph.IsAnimating);
        probe.Update(new float[] { 20, 30, 40, 50 }, new object());
        await FlushRender();
        Assert.Equal(10f, probe.Outgoing);
        var initialOffset = probe.Offset;
        var renderCount = probe.RenderCount;
        var geometryCount = probe.GeometryCount;
        var initialPixels = Pixels(probe.Graph);

        await Task.Delay(150);

        if (SystemParameters.ClientAreaAnimation)
        {
            Assert.True(probe.Graph.IsAnimating);
            Assert.InRange(probe.Offset, 0.01, initialOffset - 0.01);
            Assert.False(initialPixels.SequenceEqual(Pixels(probe.Graph)));
        }
        Assert.Equal(renderCount, probe.RenderCount);
        Assert.Equal(geometryCount, probe.GeometryCount);
        Assert.Equal(new float[] { 20, 30, 40, 50 }, probe.Values);

        await Task.Delay(450);
        Assert.False(probe.Graph.IsAnimating);
        Assert.Equal(0, probe.Offset);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DuplicateSnapshotDoesNotRestartAnimationOrReplaceHistory(bool large) => OnDispatcher(async () =>
    {
        using var fixture = new GraphFixture(large);
        var probe = fixture.Probe;
        probe.Update(new float[] { 10, 20, 30, 40 }, new object());
        await FlushRender();
        var sample = new object();
        var values = new float[] { 20, 30, 40, 50 };
        probe.Update(values, sample);
        await FlushRender();
        await Task.Delay(120);
        var offset = probe.Offset;
        var builds = probe.GeometryCount;

        probe.Update(new float[] { 20, 30, 40, 50 }, sample);
        await FlushRender();

        Assert.Same(values, probe.Values);
        Assert.True(probe.Offset <= offset);
        Assert.Equal(builds, probe.GeometryCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HiddenMinimizedAndDisabledGraphsStopAnimation(bool large) => OnDispatcher(async () =>
    {
        using var fixture = new GraphFixture(large);
        var probe = fixture.Probe;
        probe.Update(new float[] { 10, 20, 30, 40 }, new object());
        await FlushRender();
        probe.Update(new float[] { 20, 30, 40, 50 }, new object());
        await FlushRender();
        probe.Graph.Visibility = Visibility.Collapsed;
        Assert.False(probe.Graph.IsAnimating);
        Assert.Equal(0, probe.Offset);

        probe.Graph.Visibility = Visibility.Visible;
        await FlushRender();
        probe.Update(new float[] { 30, 40, 50, 60 }, new object());
        await FlushRender();
        fixture.Window.WindowState = WindowState.Minimized;
        Assert.False(probe.Graph.IsAnimating);
        Assert.Equal(0, probe.Offset);

        fixture.Window.WindowState = WindowState.Normal;
        await FlushRender();
        probe.Update(new float[] { 40, 50, 60, 70 }, new object());
        probe.Graph.IsAnimationEnabled = false;
        Assert.False(probe.Graph.IsAnimating);
        Assert.Equal(0, probe.Offset);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GrowingAndDiscontinuousHistoryDoesNotInventSamples(bool large) => OnDispatcher(async () =>
    {
        using var fixture = new GraphFixture(large);
        var probe = fixture.Probe;
        probe.Update(new float[] { 10, 20 }, new object());
        await FlushRender();
        probe.Update(new float[] { 10, 20, 30 }, new object());
        await FlushRender();
        Assert.Null(probe.Outgoing);
        Assert.Equal(3, probe.Values!.Length);

        probe.Update(new float[] { 60, 70, 80, 90 }, new object());
        await FlushRender();
        Assert.Null(probe.Outgoing);
        Assert.False(probe.Graph.IsAnimating);
        Assert.Equal(0, probe.Offset);
        Assert.Equal(new float[] { 60, 70, 80, 90 }, probe.Values);
    });

    private static async Task FlushRender()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(20);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EarlySampleRetainsTheOutgoingEdgeUntilItLeavesTheClip(bool large) => OnDispatcher(async () =>
    {
        using var fixture = new GraphFixture(large);
        var probe = fixture.Probe;
        probe.Update(new float[] { 10, 20, 30, 40 }, new object());
        await FlushRender();
        probe.Update(new float[] { 20, 30, 40, 50 }, new object());
        await FlushRender();
        await Task.Delay(80);
        probe.Update(new float[] { 30, 40, 50, 60 }, new object());
        await FlushRender();

        Assert.Equal(SystemParameters.ClientAreaAnimation ? new float[] { 10, 20 } : new float[] { 20 }, probe.Prefix);
        Assert.Equal(new float[] { 30, 40, 50, 60 }, probe.Values);
    });

    private static byte[] Pixels(FrameworkElement element)
    {
        var image = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        return pixels;
    }

    private interface IGraphProbe
    {
        AnimatedHistoryGraph Graph { get; }
        double Offset { get; }
        float? Outgoing { get; }
        IReadOnlyList<float> Prefix { get; }
        float[]? Values { get; }
        int RenderCount { get; }
        int GeometryCount { get; }
        void Update(float[] values, object sample);
    }

    private sealed class SparklineProbe : SparklineControl, IGraphProbe
    {
        public AnimatedHistoryGraph Graph => this;
        public double Offset => HistoryOffset;
        public float? Outgoing => PrimaryPrefix.Count > 0 ? PrimaryPrefix[^1] : null;
        public IReadOnlyList<float> Prefix => PrimaryPrefix;
        public int RenderCount { get; private set; }
        public int GeometryCount => GeometryBuildCount;
        public void Update(float[] values, object sample) => UpdateHistory(values, null, sample, TimeSpan.FromMilliseconds(500));
        protected override void OnRender(DrawingContext context) { RenderCount++; base.OnRender(context); }
    }

    private sealed class LargeGraphProbe : LargeHistoryGraphControl, IGraphProbe
    {
        public AnimatedHistoryGraph Graph => this;
        public double Offset => HistoryOffset;
        public float? Outgoing => PrimaryPrefix.Count > 0 ? PrimaryPrefix[^1] : null;
        public IReadOnlyList<float> Prefix => PrimaryPrefix;
        public float[]? Values => PrimaryValues;
        public int RenderCount { get; private set; }
        public int GeometryCount => GeometryBuildCount;
        public void Update(float[] values, object sample)
        {
            CurrentValue = values[^1];
            UpdateHistory(values, null, sample, TimeSpan.FromMilliseconds(500));
        }
        protected override void OnRender(DrawingContext context) { RenderCount++; base.OnRender(context); }
    }

    private sealed class GraphFixture : IDisposable
    {
        internal IGraphProbe Probe { get; }
        internal Window Window { get; }
        internal GraphFixture(bool large)
        {
            Probe = large ? new LargeGraphProbe { MaxSamples = 4 } : new SparklineProbe { MaxSamples = 4 };
            Window = new Window
            {
                Content = Probe.Graph, Width = 420, Height = 240, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual
            };
            Window.Show();
            Window.UpdateLayout();
        }
        public void Dispose() => Window.Close();
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
                try { await action(); completion.SetResult(); }
                catch (Exception exception) { completion.SetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
