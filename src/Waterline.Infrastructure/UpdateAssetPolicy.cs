using System.Security.Cryptography;

namespace Waterline.Infrastructure;

public static class UpdateAssetPolicy
{
    private const string RepositoryDownloadPrefix = "/AayyKay/waterline/releases/download/";

    public static bool TryGetTrustedInstallerUri(
        string? url,
        string? assetName,
        Version releaseVersion,
        out Uri? uri)
    {
        uri = null;
        var expectedName = $"Waterline-Setup-{releaseVersion}.exe";
        if (!string.Equals(assetName, expectedName, StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            !candidate.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.AbsolutePath.StartsWith(RepositoryDownloadPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Uri.UnescapeDataString(candidate.AbsolutePath).EndsWith($"/{expectedName}", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(candidate.UserInfo))
            return false;

        uri = candidate;
        return true;
    }

    public static bool IsTrustedReleasePage(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps &&
               uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
               uri.AbsolutePath.StartsWith("/AayyKay/waterline/releases/", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    public static bool TryNormalizeSha256(string? digest, out string normalized)
    {
        normalized = string.Empty;
        const string prefix = "sha256:";
        if (digest is null || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var candidate = digest[prefix.Length..];
        if (candidate.Length != 64 || candidate.Any(character => !Uri.IsHexDigit(character))) return false;
        normalized = candidate.ToLowerInvariant();
        return true;
    }

    public static bool HasExpectedSha256(string path, string expectedSha256)
    {
        if (!TryNormalizeSha256($"sha256:{expectedSha256}", out var normalized)) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actual = SHA256.HashData(stream);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(normalized));
    }
}
