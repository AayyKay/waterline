using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.IO;
using Waterline.Infrastructure;

namespace Waterline;

public sealed record ReleaseInfo(Version Version, string PageUrl, Uri? InstallerUri, string? InstallerSha256);

public interface IUpdateService : IDisposable
{
    Version CurrentVersion { get; }
    Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default);
    Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    void LaunchInstaller(string installerPath);
    void OpenReleasePage(string pageUrl);
}

public sealed class GitHubUpdateService : IUpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/AayyKay/waterline/releases/latest";
    private readonly HttpClient _client;

    public GitHubUpdateService(HttpMessageHandler? handler = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.Timeout = TimeSpan.FromSeconds(20);
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Waterline", CurrentVersion.ToString()));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    public async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(ReleasesApi, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (!Version.TryParse(tag, out var version)) return null;

        var candidatePage = root.GetProperty("html_url").GetString();
        var page = UpdateAssetPolicy.IsTrustedReleasePage(candidatePage)
            ? candidatePage!
            : "https://github.com/AayyKay/waterline/releases/latest";
        Uri? installer = null;
        string? installerSha256 = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString();
                var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
                if (!UpdateAssetPolicy.TryGetTrustedInstallerUri(url, name, version, out var candidate) ||
                    !UpdateAssetPolicy.TryNormalizeSha256(digest, out var normalizedDigest)) continue;
                installer = candidate;
                installerSha256 = normalizedDigest;
                break;
            }
        }
        return new ReleaseInfo(version, page, installer, installerSha256);
    }

    public async Task<string> DownloadAsync(
        ReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (release.InstallerUri is null || release.InstallerSha256 is null)
            throw new InvalidOperationException("This release does not contain a Waterline installer with a trusted SHA-256 digest.");
        var destination = Path.Combine(Path.GetTempPath(), $"Waterline-Setup-{release.Version}-{Guid.NewGuid():N}.exe");
        try
        {
            using var response = await _client.GetAsync(release.InstallerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    readTotal += read;
                    if (total is > 0) progress?.Report(readTotal * 100d / total.Value);
                }
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }
            if (!UpdateAssetPolicy.HasExpectedSha256(destination, release.InstallerSha256))
                throw new InvalidDataException("The downloaded installer did not match the SHA-256 digest published by GitHub.");
            return destination;
        }
        catch
        {
            try { if (File.Exists(destination)) File.Delete(destination); } catch { }
            throw;
        }
    }

    public void LaunchInstaller(string installerPath)
    {
        var fullPath = Path.GetFullPath(installerPath);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).StartsWith("Waterline-Setup-", StringComparison.OrdinalIgnoreCase) ||
            !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath))
            throw new InvalidOperationException("The downloaded installer path is not trusted.");
        Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
    }

    public void OpenReleasePage(string pageUrl)
    {
        if (!UpdateAssetPolicy.IsTrustedReleasePage(pageUrl)) throw new InvalidOperationException("The release page is not trusted.");
        Process.Start(new ProcessStartInfo(pageUrl) { UseShellExecute = true });
    }

    public void Dispose() => _client.Dispose();
}
