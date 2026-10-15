using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace CyberManager.UI.Services;

/// <summary>An icon shared by bindings for the same executable, including recycled rows.</summary>
public sealed class ProcessIconSource : INotifyPropertyChanged
{
    internal ProcessIconSource(Dispatcher dispatcher) => Dispatcher = dispatcher;

    public ImageSource? Source { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    internal Dispatcher Dispatcher { get; }
    internal DateTime LastWriteUtc { get; private set; }
    internal DateTimeOffset LastValidatedUtc { get; private set; }
    internal DateTimeOffset LastAccessUtc { get; set; }
    internal bool HasLoaded { get; private set; }
    internal Task PendingLoad { get; set; } = Task.CompletedTask;

    internal void Complete(ImageSource? source, DateTime lastWriteUtc, DateTimeOffset now)
    {
        Dispatcher.VerifyAccess();
        LastWriteUtc = lastWriteUtc;
        LastValidatedUtc = now;
        HasLoaded = true;
        if (ReferenceEquals(Source, source)) return;
        Source = source;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Source)));
    }
}

/// <summary>Keeps file and shell operations off the UI thread and limits extraction concurrency.</summary>
internal sealed class ProcessIconCache(
    Func<string, ImageSource?> loadIcon,
    Func<string, DateTime> getLastWriteTime,
    TimeProvider? timeProvider = null) : IDisposable
{
    private const int MaxCacheEntries = 512;
    private static readonly TimeSpan PositiveLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NegativeLifetime = TimeSpan.FromSeconds(10);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, ProcessIconSource> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly SemaphoreSlim _loadGate = new(2, 2);

    internal ProcessIconSource? Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();
        lock (_sync)
        {
            if (!_entries.TryGetValue(path, out var entry))
            {
                entry = new ProcessIconSource(Dispatcher.CurrentDispatcher);
                _entries.Add(path, entry);
            }

            var now = _time.GetUtcNow();
            entry.LastAccessUtc = now;
            var lifetime = entry.Source is null ? NegativeLifetime : PositiveLifetime;
            if (entry.PendingLoad.IsCompleted && (!entry.HasLoaded || now - entry.LastValidatedUtc >= lifetime))
            {
                // Get is called by bindings during layout: even metadata validation must run on a worker.
                entry.PendingLoad = Task.Run(() => RefreshAsync(path, entry));
            }

            EvictIfNeeded(entry);
            return entry;
        }
    }

    private async Task RefreshAsync(string path, ProcessIconSource entry)
    {
        ImageSource? source = null;
        var lastWriteUtc = DateTime.MinValue;
        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lastWriteUtc = getLastWriteTime(path);
            source = entry.Source is not null && entry.LastWriteUtc == lastWriteUtc
                ? entry.Source
                : loadIcon(path);
            // Frozen images can safely cross from the extraction worker to the UI dispatcher.
            source?.Freeze();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                         System.Runtime.InteropServices.ExternalException or ArgumentException)
        {
            Debug.WriteLine($"Unable to load process icon for '{path}': {exception}");
        }
        finally
        {
            _loadGate.Release();
        }

        if (entry.Dispatcher.HasShutdownStarted) return;
        try
        {
            await entry.Dispatcher.InvokeAsync(() =>
            {
                lock (_sync)
                {
                    entry.Complete(source, lastWriteUtc, _time.GetUtcNow());
                    EvictIfNeeded(entry);
                }
            }, DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (entry.Dispatcher.HasShutdownStarted)
        {
            // Closing the app cancels queued icon notifications.
        }
    }

    private void EvictIfNeeded(ProcessIconSource current)
    {
        if (_entries.Count <= MaxCacheEntries) return;
        var oldest = _entries.Where(pair => !ReferenceEquals(pair.Value, current) && pair.Value.HasLoaded)
            .OrderBy(pair => pair.Value.LastAccessUtc)
            .Take(_entries.Count - MaxCacheEntries)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in oldest) _entries.Remove(key);
    }

    public void Dispose() => _loadGate.Dispose();
}
