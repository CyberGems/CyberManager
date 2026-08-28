using WApp = System.Windows.Application;
using CyberManager.Common.Settings;

namespace CyberManager.UI.Services;

public static class ThemeManager
{
    public record Palette(
        string Bg, string Card, string CardSecondary, string SelectionBg, string Border, string BorderLight,
        string Text, string SubText, string Accent, string AccentGlow, string AccentFg,
        string GridAlt, string HeaderBg, string SearchBg, string InputBg, string SwitchActive, string SwitchTrack);

    private static readonly Dictionary<AppTheme, Palette> Palettes = new()
    {
        [AppTheme.CyberManager] = new("#070B12","#0E1726","#131F33","#162844","#1C2E4A","#2A436A","#F0F6FC","#8BA2C4","#00E5FF","#3300E5FF","#070B12","#0A111D","#111C2E","#0A1220","#0A1220","#00E5FF","#1C2E4A"),
        [AppTheme.Dark] = new("#121214","#1A1A1E","#222228","#282834","#2E2E38","#424250","#EDEDF0","#9E9EA8","#6366F1","#336366F1","#FFFFFF","#161619","#222229","#16161A","#16161A","#6366F1","#2E2E38"),
        [AppTheme.Light] = new("#F8FAFC","#FFFFFF","#F1F5F9","#DBEAFE","#E2E8F0","#CBD5E1","#0F172A","#64748B","#2563EB","#222563EB","#FFFFFF","#F8FAFC","#F1F5F9","#FFFFFF","#FFFFFF","#2563EB","#CBD5E1"),
    };

    public static void Apply(AppTheme theme)
    {
        var p = Palettes[theme];
        var res = WApp.Current.Resources;
        void Set(string k, string hex) => res[k] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
        Set("BgBrush", p.Bg); Set("CardBrush", p.Card); Set("CardSecondaryBrush", p.CardSecondary);
        Set("SelectionBgBrush", p.SelectionBg); Set("BorderBrush", p.Border); Set("BorderLightBrush", p.BorderLight);
        Set("TextBrush", p.Text); Set("SubTextBrush", p.SubText); Set("AccentBrush", p.Accent);
        Set("AccentGlowBrush", p.AccentGlow); Set("AccentFgBrush", p.AccentFg); Set("GridAltBrush", p.GridAlt);
        Set("HeaderBgBrush", p.HeaderBg); Set("SearchBgBrush", p.SearchBg); Set("InputBgBrush", p.InputBg);
        Set("SwitchActiveBrush", p.SwitchActive); Set("SwitchTrackBrush", p.SwitchTrack);
        if (WApp.Current.MainWindow != null) WApp.Current.MainWindow.Background = (System.Windows.Media.Brush)res["BgBrush"];
    }

    public static Palette Get(AppTheme t) => Palettes[t];
}
