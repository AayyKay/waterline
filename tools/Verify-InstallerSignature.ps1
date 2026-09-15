param(
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][string]$SigningConfigured,
    [string]$ExpectedSignerThumbprint
)

$ErrorActionPreference = 'Stop'
if ($SigningConfigured -eq 'false') {
    Write-Output 'No signing credentials were configured; unsigned release candidate is permitted.'
    return
}
if ($SigningConfigured -ne 'true' -or [string]::IsNullOrWhiteSpace($ExpectedSignerThumbprint)) {
    throw 'Signing result is missing or incomplete; refusing to publish the installer.'
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$signature = Get-AuthenticodeSignature -LiteralPath $installer
if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
    throw "Final installer does not have a valid Authenticode signature: $($signature.Status). $($signature.StatusMessage)"
}
if ($signature.SignerCertificate.Thumbprint -ne $ExpectedSignerThumbprint) {
    throw 'Final installer was not signed by the configured certificate.'
}
if ($null -eq $signature.TimeStamperCertificate) {
    throw 'Final installer signature has no trusted timestamp.'
}
Write-Output "Verified final installer signature and timestamp for signer $ExpectedSignerThumbprint."
