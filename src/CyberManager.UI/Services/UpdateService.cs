using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CyberManager.Common.I18n;

namespace CyberManager.UI.Services;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version? LatestVersion,
    string LatestVersionLabel,
    string ReleaseUrl,
    string? DownloadUrl,
    string? AssetName,
    DateTimeOffset? PublishedAt,
    bool IsUpdateAvailable,
    string StatusMessage);

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
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        _ => "win-x64"
    };

    private static readonly HttpClient GitHubHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        GitHubHttp.DefaultRequestHeaders.UserAgent.ParseAdd("CyberManager");
        GitHubHttp.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var cur = GetCurrentVersion();
        var curLabel = GetCurrentVersionLabel();
        var fallbackReleaseUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases";

        try
        {
            using var response = await GitHubHttp.GetAsync(
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest",
                ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No public releases yet on GitHub -> current build is up to date
                return new UpdateCheckResult(
                    cur,
                    cur,
                    curLabel,
                    fallbackReleaseUrl,
                    null,
                    null,
                    null,
                    false,
                    Strings.T("UpToDate", curLabel));
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = root.TryGetProperty("html_url", out var u) ? (u.GetString() ?? fallbackReleaseUrl) : fallbackReleaseUrl;
            DateTimeOffset? pub = root.TryGetProperty("published_at", out var p) ? p.GetDateTimeOffset() : null;

            var verStr = tag.TrimStart('v');
            Version? latest = Version.TryParse(verStr, out var lv) ? lv : null;

            string? downloadUrl = null;
            string? assetName = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                // Prefer installer exe
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    if (n.StartsWith("CyberManager", StringComparison.OrdinalIgnoreCase) &&
                        (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)))
                    {
                        downloadUrl = a.GetProperty("browser_download_url").GetString();
                        assetName = n;
                        break;
                    }
                }

                // Fallback to channel match
                if (downloadUrl == null)
                {
                    var channel = GetRuntimeChannel();
                    foreach (var a in assets.EnumerateArray())
                    {
                        var n = a.GetProperty("name").GetString() ?? "";
                        if (n.Contains(channel, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = a.GetProperty("browser_download_url").GetString();
                            assetName = n;
                            break;
                        }
                    }
                }
            }

            if (downloadUrl == null && root.TryGetProperty("zipball_url", out var zip))
            {
                downloadUrl = zip.GetString();
            }

            bool isAvailable = latest != null && latest > cur;
            var status = isAvailable ? Strings.T("UpdateAvailable", tag) : Strings.T("UpToDate", curLabel);

            return new UpdateCheckResult(
                cur,
                latest,
                tag,
                releaseUrl,
                downloadUrl,
                assetName,
                pub,
                isAvailable,
                status);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(
                cur,
                null,
                curLabel,
                fallbackReleaseUrl,
                null,
                null,
                null,
                false,
                Strings.T("UpdateCheckFailed"));
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult(
                cur,
                null,
                curLabel,
                fallbackReleaseUrl,
                null,
                null,
                null,
                false,
                Strings.T("UpdateCheckTimeout"));
        }
        catch
        {
            return new UpdateCheckResult(
                cur,
                null,
                curLabel,
                fallbackReleaseUrl,
                null,
                null,
                null,
                false,
                Strings.T("UnexpectedResponse"));
        }
    }

    public static async Task DownloadUpdateAsync(string downloadUrl, string destinationPath, IProgress<double> progress, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var response = await GitHubHttp.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percentage = (double)totalRead / totalBytes * 100.0;
                progress.Report(percentage);
            }
        }
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        };
        Process.Start(psi);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public static void LaunchReleasesPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) url = $"https://github.com/{RepoOwner}/{RepoName}/releases";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
