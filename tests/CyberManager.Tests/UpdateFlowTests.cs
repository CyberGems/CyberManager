using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class UpdateFlowTests
{
    [Fact]
    public void PreviewFollowsTheInterfaceLanguageAndOmitsInstallerInstructions()
    {
        const string body = """
            ### Highlights
            - 📋 **Copy process details**:
              - A nested point that does not belong in the preview
            - **Shortcuts**
            - **Theme cards**
            - **New toggles**
            - An extra highlight
            <details><summary><b>Español</b></summary>
            ### Novedades
            - **Copiar detalles**
            ### ℹ️ Antes de instalar
            - Instrucciones de instalación
            </details>
            ### ℹ️ Before you install
            - Installer instructions
            """;

        Assert.Collection(ReleaseNotes.Peek(body, Lang.En),
            item => Assert.Equal("Copy process details", item),
            item => Assert.Equal("Shortcuts", item),
            item => Assert.Equal("Theme cards", item),
            item => Assert.Equal("New toggles", item));
        Assert.Equal("Copiar detalles", Assert.Single(ReleaseNotes.Peek(body, Lang.Es)));
    }

    [Fact]
    public async Task DownloadPublishesTheVerifiedFileAndItsFinalByteCount()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "update.exe");
        var bytes = new byte[65_537];
        var progress = new ProgressLog();
        using var client = new HttpClient(new AssetResponse(new ByteArrayContent(bytes)));

        await UpdateService.DownloadAssetAsync(client, new Uri("https://example.invalid/update"), file,
            progress, Convert.ToHexString(SHA256.HashData(bytes)), CancellationToken.None);

        Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(new UpdateDownloadProgress(0, bytes.Length), progress.Values.First());
        Assert.Equal(new UpdateDownloadProgress(bytes.Length, bytes.Length), progress.Values.Last());
        Assert.Equal(file, Assert.Single(Directory.EnumerateFiles(directory.Path)));
    }

    [Fact]
    public async Task CancellationStopsTheTransferAndRemovesThePartialFile()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        await using var stream = new PausedDownloadStream();
        using var client = new HttpClient(new AssetResponse(new StreamContent(stream)));
        var file = Path.Combine(directory.Path, "update.exe");

        var transfer = UpdateService.DownloadAssetAsync(client, new Uri("https://example.invalid/update"), file,
            new ProgressLog(), expectedSha256: null, cancellation.Token);
        await stream.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transfer);
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task BadChecksumLeavesAnExistingVerifiedPackageIntact()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "update.exe");
        byte[] existing = [1, 2, 3];
        File.WriteAllBytes(file, existing);
        using var client = new HttpClient(new AssetResponse(new ByteArrayContent([4, 5, 6])));

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.DownloadAssetAsync(client,
            new Uri("https://example.invalid/update"), file, new ProgressLog(),
            Convert.ToHexString(SHA256.HashData(existing)), CancellationToken.None));

        Assert.Equal(existing, File.ReadAllBytes(file));
        Assert.Equal(file, Assert.Single(Directory.EnumerateFiles(directory.Path)));
    }

    private sealed class AssetResponse(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class ProgressLog : IProgress<UpdateDownloadProgress>
    {
        public List<UpdateDownloadProgress> Values { get; } = [];
        public void Report(UpdateDownloadProgress value) => Values.Add(value);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("CyberManager-update-test-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class PausedDownloadStream() : MemoryStream(new byte[70_000])
    {
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0) return await base.ReadAsync(buffer, cancellationToken);
            Paused.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
