using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CyberManager.UI.Services;

public sealed record UpdateCheckResult(Version CurrentVersion, Version? LatestVersion, string LatestVersionLabel, string ReleaseUrl, string? DownloadUrl, string? AssetName, DateTimeOffset? PublishedAt, bool IsUpdateAvailable, string StatusMessage);

public static class UpdateService
{
    private const string RepoOwner = "CyberGems";
    private const string RepoName = "CyberManager";

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? new Version(1, 0, 0) : new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    public static string GetCurrentVersionLabel()
    {
        var v = GetCurrentVersion();
        return v.Revision > 0 ? $"v{v}" : $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    public static string GetRuntimeChannel() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.Arm64 => "win-arm64",
        _ => "win-x64"
    };

    private static readonly HttpClient GitHubHttp = new() { Timeout = TimeSpan.FromSeconds(12) };
    static UpdateService() { GitHubHttp.DefaultRequestHeaders.UserAgent.ParseAdd("CyberManager"); GitHubHttp.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var cur = GetCurrentVersion();
        var curLabel = GetCurrentVersionLabel();
        try
        {
            var json = await GitHubHttp.GetStringAsync($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.GetProperty("html_url").GetString() ?? "";
            DateTimeOffset? pub = root.TryGetProperty("published_at", out var p) ? p.GetDateTimeOffset() : null;
            var verStr = tag.TrimStart('v');
            Version? latest = Version.TryParse(verStr, out var lv) ? lv : null;
            string? dl = null; string? name = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    if (n.StartsWith("CyberManager", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    { dl = a.GetProperty("browser_download_url").GetString(); name = n; break; }
                }
            }
            bool avail = latest != null && latest > cur;
            var status = avail ? $"Actualización {tag} disponible" : $"Estás al día con la versión {curLabel}";
            return new(cur, latest, tag, url, dl, name, pub, avail, status);
        }
        catch (HttpRequestException) { return new(cur, null, curLabel, "", null, null, null, false, "No se pudo comprobar actualizaciones. Verifica tu conexión."); }
        catch (TaskCanceledException) { return new(cur, null, curLabel, "", null, null, null, false, "Tiempo agotado al comprobar actualizaciones."); }
        catch { return new(cur, null, curLabel, "", null, null, null, false, "Respuesta inesperada del servidor."); }
    }

    public static void LaunchReleasesPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) url = $"https://github.com/{RepoOwner}/{RepoName}/releases";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
