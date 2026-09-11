using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    string? Sha256,
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
            string? sha256 = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                var channel = GetRuntimeChannel();
                var availableAssets = assets.EnumerateArray().ToList();
                var preferredAssets = availableAssets
                    .Where(asset => IsSupportedAsset(
                        asset.GetProperty("name").GetString() ?? "",
                        channel,
                        preferInstaller: channel == "win-x64"))
                    .ToList();

                if (preferredAssets.Count == 0)
                {
                    preferredAssets = availableAssets
                        .Where(asset => IsSupportedAsset(
                            asset.GetProperty("name").GetString() ?? "",
                            channel,
                            preferInstaller: false))
                        .ToList();
                }

                foreach (var a in preferredAssets)
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    downloadUrl = a.GetProperty("browser_download_url").GetString();
                    assetName = n;
                    sha256 = ReadSha256(a);
                    break;
                }
            }

            bool newerVersion = latest != null && latest > cur;
            bool isAvailable = newerVersion && downloadUrl != null;
            var status = !newerVersion
                ? Strings.T("UpToDate", curLabel)
                : isAvailable
                    ? Strings.T("UpdateAvailable", tag)
                    : Strings.T("UpdatePackageUnavailable");

            return new UpdateCheckResult(
                cur,
                latest,
                tag,
                releaseUrl,
                downloadUrl,
                assetName,
                sha256,
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
                null,
                false,
                Strings.T("UnexpectedResponse"));
        }
    }

    public static async Task DownloadUpdateAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double> progress,
        string? expectedSha256 = null,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IsAllowedDownloadHost(uri.Host))
        {
            throw new InvalidDataException("The update URL is not a trusted HTTPS GitHub download.");
        }

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temporaryPath = destinationPath + ".download";
        try
        {
            using var response = await GitHubHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);

            var buffer = new byte[64 * 1024];
            var totalRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    progress.Report((double)totalRead / totalBytes * 100.0);
                }
            }

            await fileStream.FlushAsync(ct).ConfigureAwait(false);
            await fileStream.DisposeAsync().ConfigureAwait(false);
            await VerifyDownloadedAssetAsync(temporaryPath, Path.GetExtension(destinationPath), expectedSha256, ct);
            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { }
        }
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        var extension = Path.GetExtension(installerPath);
        if (!File.Exists(installerPath) ||
            (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
             !extension.Equals(".msi", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The downloaded update is not an installable CyberManager package.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ? "msiexec.exe" : installerPath,
            Arguments = extension.Equals(".msi", StringComparison.OrdinalIgnoreCase)
                ? $"/i \"{installerPath}\" /quiet /norestart"
                : "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART",
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

    private static bool IsSupportedAsset(string name, string channel, bool preferInstaller)
    {
        if (preferInstaller)
        {
            return name.StartsWith("CyberManager-Setup-", StringComparison.OrdinalIgnoreCase) &&
                   (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
        }

        return name.StartsWith("CyberManager-", StringComparison.OrdinalIgnoreCase) &&
               name.Contains($"-Portable-{channel}", StringComparison.OrdinalIgnoreCase) &&
               name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadSha256(JsonElement asset)
    {
        if (!asset.TryGetProperty("digest", out var digestElement)) return null;
        var digest = digestElement.GetString();
        if (string.IsNullOrWhiteSpace(digest)) return null;
        const string prefix = "sha256:";
        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digest[prefix.Length..]
            : null;
    }

    private static bool IsAllowedDownloadHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static async Task VerifyDownloadedAssetAsync(
        string path,
        string assetExtension,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            await using var stream = File.OpenRead(path);
            var actual = await SHA256.HashDataAsync(stream, cancellationToken);
            var expected = Convert.FromHexString(expectedSha256.Trim());
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
            }

            return;
        }

        // An executable without a release digest must at least carry an
        // Authenticode certificate before it can be handed to the shell.
        if (assetExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            assetExtension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is required to inspect embedded Authenticode metadata.
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                using var chain = new X509Chain();
                if (!chain.Build(certificate))
                {
                    throw new InvalidDataException("The update Authenticode certificate is not trusted.");
                }
            }
            catch (CryptographicException ex)
            {
                throw new InvalidDataException("The downloaded update has no verifiable Authenticode signature.", ex);
            }
        }
        else
        {
            throw new InvalidDataException("The release did not provide a SHA-256 digest for this update.");
        }
    }
}
