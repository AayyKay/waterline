using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.IO;

namespace Waterline;

public sealed record ReleaseInfo(Version Version, string PageUrl, string? InstallerUrl);

public sealed class GitHubUpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/AayyKay/waterline/releases/latest";
    private readonly HttpClient _client = new();

    public GitHubUpdateService()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Waterline", CurrentVersion.ToString()));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    public async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(ReleasesApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (!Version.TryParse(tag, out var version)) return null;
        var page = root.GetProperty("html_url").GetString() ?? "https://github.com/AayyKay/waterline/releases/latest";
        string? installer = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name?.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) == true ||
                name?.StartsWith("Waterline-Setup-", StringComparison.OrdinalIgnoreCase) == true)
            {
                installer = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }
        return new ReleaseInfo(version, page, installer);
    }

    public async Task DownloadAndInstallAsync(ReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (release.InstallerUrl is null)
        {
            Process.Start(new ProcessStartInfo(release.PageUrl) { UseShellExecute = true });
            return;
        }

        var destination = Path.Combine(Path.GetTempPath(), $"Waterline-Setup-{release.Version}.exe");
        using var response = await _client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destination);
        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            readTotal += read;
            if (total is > 0) progress?.Report(readTotal * 100d / total.Value);
        }
        Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
    }
}
