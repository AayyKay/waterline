param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$InstallerPath
)

$ErrorActionPreference = 'Stop'
$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$executable = Join-Path $publishRoot 'Waterline.exe'
$license = Join-Path $publishRoot 'licenses\LevelDB.Standard-LICENSE.txt'
$failures = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { $failures.Add('Published Waterline.exe is missing.') }
if (-not (Test-Path -LiteralPath $license -PathType Leaf)) { $failures.Add('The LevelDB.Standard license is missing from the published payload.') }
if ([System.IO.Path]::GetFileName($installer) -ne "Waterline-Setup-$Version.exe") { $failures.Add('Installer filename does not match the project version.') }

if (Test-Path -LiteralPath $executable -PathType Leaf) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
    if (-not $info.FileVersion.StartsWith("$Version.")) { $failures.Add("Waterline.exe reports file version $($info.FileVersion), expected $Version.") }
    $bytes = [System.IO.File]::ReadAllBytes($executable)
    if ($bytes.Length -lt 1MB -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) { $failures.Add('Published Waterline.exe is not a valid self-contained Windows executable.') }
}

$installerBytes = [System.IO.File]::ReadAllBytes($installer)
if ($installerBytes.Length -lt 1MB -or $installerBytes[0] -ne 0x4d -or $installerBytes[1] -ne 0x5a) {
    $failures.Add('The generated installer is not a valid Windows executable.')
}

if ($failures.Count -eq 0) {
    $validationRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Waterline-RC-" + [guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($validationRoot) | Out-Null
    try {
        $snapshot = Join-Path $validationRoot 'empty.png'
        $process = Start-Process -FilePath $executable -ArgumentList @('--snapshot', $snapshot, 'empty') -PassThru
        if (-not $process.WaitForExit(20000)) {
            $process.Kill($true)
            $failures.Add('Published Waterline.exe did not finish its isolated startup smoke test within 20 seconds.')
        } elseif ($process.ExitCode -ne 0) {
            $failures.Add("Published Waterline.exe exited with code $($process.ExitCode) during startup smoke testing.")
        } elseif (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) {
            $failures.Add('Published Waterline.exe did not produce the isolated startup snapshot.')
        } else {
            $png = [System.IO.File]::ReadAllBytes($snapshot)
            if ($png.Length -lt 8 -or $png[0] -ne 0x89 -or $png[1] -ne 0x50 -or $png[2] -ne 0x4e -or $png[3] -ne 0x47) {
                $failures.Add('The startup smoke-test output is not a valid PNG.')
            }
        }
    } finally {
        $resolvedValidation = [System.IO.Path]::GetFullPath($validationRoot)
        $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($resolvedValidation.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($resolvedValidation).StartsWith('Waterline-RC-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedValidation -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

$installerHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Verified Waterline $Version release candidate."
Write-Output "Executable: $executable"
Write-Output "Installer: $installer"
Write-Output "Installer SHA-256: $installerHash"
