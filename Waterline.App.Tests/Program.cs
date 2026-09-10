using System.Net;
using System.Security.Cryptography;
using System.Text;
using Waterline;

var tests = new List<(string Name, Action Run)>();
var failures = new List<string>();

Test("accepts a versioned installer only when GitHub supplies its digest", () =>
{
    var payload = Encoding.UTF8.GetBytes("candidate installer");
    var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    using var updates = new GitHubUpdateService(new StubHandler((_, _) => Task.FromResult(ResponseJson(ReleaseJson($"sha256:{digest}")))));
    var release = updates.CheckAsync().GetAwaiter().GetResult();
    Equal(new Version(2, 2, 0), release!.Version);
    Equal(digest, release.InstallerSha256);
    Equal("github.com", release.InstallerUri!.Host);
});

Test("does not offer an installer when release integrity metadata is absent", () =>
{
    using var updates = new GitHubUpdateService(new StubHandler((_, _) => Task.FromResult(ResponseJson(ReleaseJson(null)))));
    var release = updates.CheckAsync().GetAwaiter().GetResult();
    Equal<Uri?>(null, release!.InstallerUri);
    Equal<string?>(null, release.InstallerSha256);
});

Test("surfaces malformed and offline release responses", () =>
{
    using var malformed = new GitHubUpdateService(new StubHandler((_, _) => Task.FromResult(ResponseJson("{}"))));
    ThrowsAny(() => malformed.CheckAsync().GetAwaiter().GetResult());
    using var offline = new GitHubUpdateService(new StubHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))));
    ThrowsAny(() => offline.CheckAsync().GetAwaiter().GetResult());
});

Test("downloads a verified installer and closes it before returning", () =>
{
    var payload = Encoding.UTF8.GetBytes("verified Waterline installer bytes");
    var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    using var updates = new GitHubUpdateService(new StubHandler((_, _) => Task.FromResult(ResponseBytes(payload))));
    var release = CandidateRelease(new Version(9, 8, 7), digest);
    var path = updates.DownloadAsync(release).GetAwaiter().GetResult();
    try
    {
        Equal(true, File.ReadAllBytes(path).SequenceEqual(payload));
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
});

Test("deletes a download that fails digest verification", () =>
{
    var version = new Version(9, 8, 6);
    var pattern = $"Waterline-Setup-{version}-*.exe";
    var before = Directory.GetFiles(Path.GetTempPath(), pattern).ToHashSet(StringComparer.OrdinalIgnoreCase);
    using var updates = new GitHubUpdateService(new StubHandler((_, _) => Task.FromResult(ResponseBytes(Encoding.UTF8.GetBytes("tampered")))));
    ThrowsAny(() => updates.DownloadAsync(CandidateRelease(version, new string('0', 64))).GetAwaiter().GetResult());
    var after = Directory.GetFiles(Path.GetTempPath(), pattern).Where(path => !before.Contains(path)).ToList();
    Equal(0, after.Count);
});

Test("honors cancellation without leaving a partial installer", () =>
{
    var version = new Version(9, 8, 5);
    var pattern = $"Waterline-Setup-{version}-*.exe";
    var before = Directory.GetFiles(Path.GetTempPath(), pattern).ToHashSet(StringComparer.OrdinalIgnoreCase);
    using var updates = new GitHubUpdateService(new StubHandler(async (_, token) =>
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return ResponseBytes(Array.Empty<byte>());
    }));
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
    ThrowsType<OperationCanceledException>(() => updates.DownloadAsync(CandidateRelease(version, new string('0', 64)), cancellationToken: cancellation.Token).GetAwaiter().GetResult());
    var after = Directory.GetFiles(Path.GetTempPath(), pattern).Where(path => !before.Contains(path)).ToList();
    Equal(0, after.Count);
});

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} of {tests.Count} app checks failed.");
    return 1;
}

Console.WriteLine($"All {tests.Count} automated Waterline app checks passed.");
return 0;

void Test(string name, Action run) => tests.Add((name, run));

static ReleaseInfo CandidateRelease(Version version, string digest) => new(
    version,
    $"https://github.com/AayyKay/waterline/releases/tag/v{version}",
    new Uri($"https://github.com/AayyKay/waterline/releases/download/v{version}/Waterline-Setup-{version}.exe"),
    digest);

static string ReleaseJson(string? digest)
{
    var digestProperty = digest is null ? string.Empty : $",\"digest\":\"{digest}\"";
    return $$"""
    {
      "tag_name": "v2.2.0",
      "html_url": "https://github.com/AayyKay/waterline/releases/tag/v2.2.0",
      "assets": [{
        "name": "Waterline-Setup-2.2.0.exe",
        "browser_download_url": "https://github.com/AayyKay/waterline/releases/download/v2.2.0/Waterline-Setup-2.2.0.exe"{{digestProperty}}
      }]
    }
    """;
}

static HttpResponseMessage ResponseJson(string json)
{
    var response = ResponseBytes(Encoding.UTF8.GetBytes(json));
    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    return response;
}

static HttpResponseMessage ResponseBytes(byte[] bytes) => new(HttpStatusCode.OK)
{
    Content = new ByteArrayContent(bytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream") } }
};

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}

static void ThrowsAny(Action action) => ThrowsType<Exception>(action);

static void ThrowsType<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        response(request, cancellationToken);
}
