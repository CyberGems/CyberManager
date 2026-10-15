using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.Common.Models;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class ProcessIconCacheTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public Task LookupDoesNotBlockUiAndSharesPendingLoads() => OnDispatcher(async () =>
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var metadataThread = 0;
        var extractionThread = 0;
        var notificationsThread = 0;
        var loads = 0;
        var icon = CreateIcon(10);
        using var cache = new ProcessIconCache(_ =>
        {
            extractionThread = Environment.CurrentManagedThreadId;
            Interlocked.Increment(ref loads);
            started.SetResult();
            if (!release.Wait(Timeout)) throw new TimeoutException();
            return icon;
        }, _ =>
        {
            metadataThread = Environment.CurrentManagedThreadId;
            return DateTime.UnixEpoch;
        });

        var entry = cache.Get("app.exe")!;
        entry.PropertyChanged += (_, _) => notificationsThread = Environment.CurrentManagedThreadId;
        try
        {
            await started.Task.WaitAsync(Timeout);
            Assert.Null(entry.Source);
            Assert.Same(entry, cache.Get(" APP.EXE "));
            Assert.Equal(1, loads);
            Assert.NotEqual(uiThread, metadataThread);
            Assert.NotEqual(uiThread, extractionThread);
        }
        finally
        {
            release.Set();
            await entry.PendingLoad.WaitAsync(Timeout);
        }

        Assert.Same(icon, entry.Source);
        Assert.True(Assert.IsAssignableFrom<ImageSource>(entry.Source).IsFrozen);
        Assert.Equal(uiThread, notificationsThread);
        Assert.Same(entry, cache.Get("app.exe"));
        Assert.Equal(1, loads);
    });

    [Fact]
    public Task MissingIconRetriesDespiteContinuousAccess() => OnDispatcher(async () =>
    {
        var clock = new TestTimeProvider();
        var attempts = 0;
        var icon = CreateIcon(20);
        using var cache = new ProcessIconCache(_ => ++attempts == 1 ? null : icon,
            _ => DateTime.UnixEpoch, clock);

        var entry = cache.Get("app.exe")!;
        await entry.PendingLoad.WaitAsync(Timeout);
        for (var second = 1; second < 10; second++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Same(entry, cache.Get("app.exe"));
            Assert.Null(entry.Source);
            Assert.Equal(1, attempts);
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Same(entry, cache.Get("app.exe"));
        await entry.PendingLoad.WaitAsync(Timeout);
        Assert.Same(icon, entry.Source);
        Assert.Equal(2, attempts);
    });

    [Fact]
    public Task RevalidationChecksMetadataWithoutExtractingUnchangedIcons() => OnDispatcher(async () =>
    {
        var clock = new TestTimeProvider();
        var loads = 0;
        var validations = 0;
        var icon = CreateIcon(30);
        using var cache = new ProcessIconCache(_ => { loads++; return icon; },
            _ => { validations++; return DateTime.UnixEpoch; }, clock);
        var entry = cache.Get("app.exe")!;
        await entry.PendingLoad.WaitAsync(Timeout);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Same(entry, cache.Get("app.exe"));
        Assert.Same(icon, entry.Source);
        await entry.PendingLoad.WaitAsync(Timeout);

        Assert.Equal(2, validations);
        Assert.Equal(1, loads);
        Assert.Same(icon, entry.Source);
    });

    [Fact]
    public Task ChangedExecutableKeepsOldIconWhileLoadingAndClearsUnavailableIcon() => OnDispatcher(async () =>
    {
        var clock = new TestTimeProvider();
        var icon = CreateIcon(40);
        var modified = DateTime.UnixEpoch;
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cache = new ProcessIconCache(_ =>
        {
            if (modified == DateTime.UnixEpoch) return icon;
            started.SetResult();
            if (!release.Wait(Timeout)) throw new TimeoutException();
            return null;
        }, _ => modified, clock);
        var entry = cache.Get("app.exe")!;
        await entry.PendingLoad.WaitAsync(Timeout);
        var notifications = 0;
        entry.PropertyChanged += (_, _) => notifications++;

        modified = modified.AddSeconds(1);
        clock.Advance(TimeSpan.FromMinutes(5));
        cache.Get("app.exe");
        try
        {
            await started.Task.WaitAsync(Timeout);
            Assert.Same(icon, entry.Source);
        }
        finally
        {
            release.Set();
            await entry.PendingLoad.WaitAsync(Timeout);
        }

        Assert.Null(entry.Source);
        Assert.Equal(1, notifications);
    });

    [Fact]
    public Task ExtractionConcurrencyIsBoundedAcrossDifferentPaths() => OnDispatcher(async () =>
    {
        using var release = new ManualResetEventSlim();
        var twoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new object();
        var active = 0;
        var peak = 0;
        var loads = 0;
        using var cache = new ProcessIconCache(_ =>
        {
            lock (sync)
            {
                active++;
                loads++;
                peak = Math.Max(peak, active);
                if (active == 2) twoStarted.TrySetResult();
            }
            try
            {
                if (!release.Wait(Timeout)) throw new TimeoutException();
                return CreateIcon(50);
            }
            finally
            {
                lock (sync) active--;
            }
        }, _ => DateTime.UnixEpoch);
        var entries = Enumerable.Range(0, 8).Select(index => cache.Get($"app{index}.exe")!).ToList();
        try
        {
            await twoStarted.Task.WaitAsync(Timeout);
            lock (sync) Assert.Equal(2, active);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(entries.Select(entry => entry.PendingLoad)).WaitAsync(Timeout);
        }

        Assert.Equal(8, loads);
        Assert.Equal(2, peak);
        Assert.All(entries, entry => Assert.NotNull(entry.Source));
    });

    [Fact]
    public Task RecycledImageIgnoresLateIconFromItsPreviousProcess() => OnDispatcher(async () =>
    {
        using var releaseFirst = new ManualResetEventSlim();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstIcon = CreateIcon(60);
        var secondIcon = CreateIcon(70);
        using var cache = new ProcessIconCache(path =>
        {
            if (path == "second.exe") return secondIcon;
            firstStarted.SetResult();
            if (!releaseFirst.Wait(Timeout)) throw new TimeoutException();
            return firstIcon;
        }, _ => DateTime.UnixEpoch);
        var converter = new PathToIconConverter(cache);
        var host = new Grid { DataContext = new ProcessInfo { ExePath = "first.exe" } };
        var image = new Image();
        host.Children.Add(image);
        image.SetBinding(System.Windows.FrameworkElement.DataContextProperty,
            new Binding("ExePath") { Converter = converter, ConverterCulture = CultureInfo.InvariantCulture });
        image.SetBinding(Image.SourceProperty, new Binding(nameof(ProcessIconSource.Source)));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var firstEntry = cache.Get("first.exe")!;
        try
        {
            await firstStarted.Task.WaitAsync(Timeout);
            host.DataContext = new ProcessInfo { ExePath = "second.exe" };
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var secondEntry = cache.Get("second.exe")!;
            await secondEntry.PendingLoad.WaitAsync(Timeout);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Same(secondIcon, image.Source);
        }
        finally
        {
            releaseFirst.Set();
            await firstEntry.PendingLoad.WaitAsync(Timeout);
        }

        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.Same(secondIcon, image.Source);
        Assert.Same(firstIcon, firstEntry.Source);
    });

    private static BitmapSource CreateIcon(byte value)
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { value, value, value, 255 }, 4);
        bitmap.Freeze();
        return bitmap;
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
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan amount) => _now += amount;
    }
}
