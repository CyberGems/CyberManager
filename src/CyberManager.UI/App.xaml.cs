using System.Windows;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Settings = AppSettings.Load();
        Strings.Current = Settings.Language;
        base.OnStartup(e);
        ThemeManager.Apply(Settings.Theme);
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
