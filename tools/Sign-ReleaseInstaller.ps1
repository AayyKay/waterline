param(
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][string]$OutputFile
)

$ErrorActionPreference = 'Stop'
$pfxBase64 = $env:WATERLINE_SIGNING_PFX_BASE64
$pfxPassword = $env:WATERLINE_SIGNING_PFX_PASSWORD

if ([string]::IsNullOrEmpty($pfxBase64) -and [string]::IsNullOrEmpty($pfxPassword)) {
    Add-Content -LiteralPath $OutputFile -Value 'signed=false'
    Write-Output 'Signing credentials are absent; leaving the release candidate unsigned.'
    return
}
if ([string]::IsNullOrEmpty($pfxBase64) -or [string]::IsNullOrEmpty($pfxPassword)) {
    throw 'Both WATERLINE_SIGNING_PFX_BASE64 and WATERLINE_SIGNING_PFX_PASSWORD must be configured together.'
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$pfxBytes = [Convert]::FromBase64String($pfxBase64)
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $pfxBytes,
    $pfxPassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
)
try {
    $codeSigningOid = '1.3.6.1.5.5.7.3.3'
    $hasCodeSigningEku = $false
    foreach ($extension in $certificate.Extensions) {
        if ($extension -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
            foreach ($usage in $extension.EnhancedKeyUsages) {
                if ($usage.Value -eq $codeSigningOid) { $hasCodeSigningEku = $true }
            }
        }
    }
    if (-not $certificate.HasPrivateKey -or -not $hasCodeSigningEku) {
        throw 'The PFX must contain a private key and a certificate with the code-signing extended key usage.'
    }
    if ([DateTime]::UtcNow -lt $certificate.NotBefore.ToUniversalTime() -or
        [DateTime]::UtcNow -gt $certificate.NotAfter.ToUniversalTime()) {
        throw 'The code-signing certificate is not currently valid.'
    }

    $result = Set-AuthenticodeSignature -LiteralPath $installer -Certificate $certificate `
        -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com' -ErrorAction Stop
    if ($result.Status -ne 'Valid') {
        throw "Installer signing failed: $($result.Status). $($result.StatusMessage)"
    }

    Add-Content -LiteralPath $OutputFile -Value 'signed=true'
    Add-Content -LiteralPath $OutputFile -Value "signer_thumbprint=$($certificate.Thumbprint)"
    Write-Output "Signed installer with certificate thumbprint $($certificate.Thumbprint)."
} finally {
    $certificate.Dispose()
    [Array]::Clear($pfxBytes, 0, $pfxBytes.Length)
}
