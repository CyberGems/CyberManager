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
    string StatusKey,
    string? ReleaseNotes = null)
{
    public bool IsUpToDate => LatestVersion is { } latest && latest <= CurrentVersion;
    public string StatusMessage => StatusKey switch
    {
        "UpToDate" => Strings.T(StatusKey, UpdateService.GetCurrentVersionLabel()),
        "UpdateAvailable" or "UpdateTagUnrecognized" => Strings.T(StatusKey, LatestVersionLabel),
        _ => Strings.T(StatusKey)
    };
}

internal readonly record struct UpdateDownloadProgress(long Downloaded, long? Total);

public static class UpdateService
{
    private const string RepoOwner = "CyberGems";
    private const string RepoName = "CyberManager";

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? new Version(1, 0, 0) : Normalize(v);
    }

    /// <summary>
    /// Brings a version to the four-part shape the assembly version always has, so comparisons
    /// against a three-part release tag ("v1.2.3" carries Revision -1) stay meaningful.
    /// </summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>
    /// Parses a GitHub release tag into a comparable version.
    /// <see cref="Version.TryParse"/> rejects any tag carrying a prerelease or build suffix
    /// ("v1.2.3-beta.1"), and a tag it cannot parse used to be reported as "up to date", which
    /// hides a real release. The numeric core is extracted first so those tags still compare.
    /// Returns <c>false</c> when the tag holds no numeric version at all, which is a genuinely
    /// unknown state the caller must report instead of assuming the build is current.
    /// </summary>
    public static bool TryParseReleaseTag(string? tag, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var span = tag.Trim();
        if (span.Length > 0 && (span[0] == 'v' || span[0] == 'V')) span = span[1..];

        // The numeric core is the leading run of digits and dots; everything after it is a
        // prerelease or build label that must not take part in the comparison.
        var end = 0;
        while (end < span.Length && (char.IsAsciiDigit(span[end]) || span[end] == '.')) end++;

        var core = span[..end].TrimEnd('.');
        if (core.Length == 0) return false;

        if (!Version.TryParse(core, out var parsed)) return false;
        version = Normalize(parsed);
        return true;
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
        // ARM64 devices use the supported x64 package through Windows emulation.
        Architecture.Arm64 => "win-x64",
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
                    "UpToDate");
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = root.TryGetProperty("html_url", out var u) ? (u.GetString() ?? fallbackReleaseUrl) : fallbackReleaseUrl;
            DateTimeOffset? pub = root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
                && p.TryGetDateTimeOffset(out var published) ? published : null;
            var notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString() : null;

            if (string.IsNullOrWhiteSpace(tag))
            {
                // A release without a tag cannot be compared or downloaded.
                return new UpdateCheckResult(
                    cur,
                    null,
                    curLabel,
                    releaseUrl,
                    null,
                    null,
                    null,
                    pub,
                    false,
                    "UnexpectedResponse");
            }

            var tagIsComparable = TryParseReleaseTag(tag, out var latest);
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

            bool newerVersion = latest is not null && latest > cur;
            bool isAvailable = newerVersion && downloadUrl != null;
            // An unparsable tag is reported as unknown rather than as "up to date": claiming the
            // build is current when the comparison never happened would hide a real release.
            var status = !tagIsComparable
                ? "UpdateTagUnrecognized"
                : !newerVersion
                    ? "UpToDate"
                    : isAvailable
                        ? "UpdateAvailable"
                        : "UpdatePackageUnavailable";

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
                status,
                notes);
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
                "UpdateCheckFailed");
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
                "UpdateCheckTimeout");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Debug.WriteLine($"Unexpected update response: {ex}");
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
                "UnexpectedResponse");
        }
    }

    internal static Task DownloadUpdateAsync(string downloadUrl, string destinationPath,
        IProgress<UpdateDownloadProgress> progress, string? expectedSha256, CancellationToken ct)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !IsAllowedDownloadHost(uri.Host))
            throw new InvalidDataException("The update URL is not a trusted HTTPS GitHub download.");

        return DownloadAssetAsync(GitHubHttp, uri, destinationPath, progress, expectedSha256, ct);
    }

    internal static async Task DownloadAssetAsync(HttpClient client, Uri uri, string destinationPath,
        IProgress<UpdateDownloadProgress> progress, string? expectedSha256, CancellationToken ct)
    {
        if (Path.GetDirectoryName(destinationPath) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);

        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.download";
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            await using var content = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            long downloaded = 0;
            var lastReport = Stopwatch.StartNew();
            progress.Report(new UpdateDownloadProgress(0, totalBytes));
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    downloaded += read;
                    if (lastReport.ElapsedMilliseconds < 120) continue;
                    progress.Report(new UpdateDownloadProgress(downloaded, totalBytes));
                    lastReport.Restart();
                }
            }

            if (downloaded == 0 || totalBytes is long expected && downloaded != expected)
                throw new InvalidDataException("The downloaded update is empty or incomplete.");
            await VerifyDownloadedAssetAsync(temporaryPath, Path.GetExtension(destinationPath), expectedSha256, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destinationPath, overwrite: true);
            progress.Report(new UpdateDownloadProgress(downloaded, totalBytes ?? downloaded));
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Update download cleanup failed: {ex}");
            }
        }
    }

    public static void LaunchInstaller(string installerPath)
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
        using var installer = Process.Start(psi) ?? throw new InvalidOperationException("The installer could not start.");
    }

    public static void LaunchReleasesPage(string url)
    {
        var fallback = $"https://github.com/{RepoOwner}/{RepoName}/releases";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !(uri.AbsolutePath.Equals($"/{RepoOwner}/{RepoName}/releases", StringComparison.OrdinalIgnoreCase) ||
              uri.AbsolutePath.StartsWith($"/{RepoOwner}/{RepoName}/releases/", StringComparison.OrdinalIgnoreCase)))
            url = fallback;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
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
