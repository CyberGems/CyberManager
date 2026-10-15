using System.Windows;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    /// <summary>Owns the single-instance mutex for the whole session; also carries the wake-up hook.</summary>
    public static SingleInstance SingleInstance { get; private set; } = null!;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // One instance only: a second launch wakes this window instead of opening another copy.
        SingleInstance = new SingleInstance();
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExistingInstance();
            Shutdown();
            return;
        }

        Settings = AppSettings.Load();
        Strings.Current = Settings.Language;
        ThemeManager.Apply(Settings.Theme);
        _ = Task.Run(() =>
            StartupManager.EnsureAutoStart(Settings.StartWithWindows, startMinimized: true));
        var w = new MainWindow();
        MainWindow = w;
        w.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try { Settings.Save(); } catch { }
        base.OnExit(e);
    }
}
