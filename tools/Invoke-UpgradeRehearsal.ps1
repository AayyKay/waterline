param(
    [Parameter(Mandatory = $true)][string]$BaselineInstaller,
    [Parameter(Mandatory = $true)][string]$CandidateInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedBaselineSha256,
    [Parameter(Mandatory = $true)][string]$RehearsalRoot
)

$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselineInstaller).Path
$candidate = (Resolve-Path -LiteralPath $CandidateInstaller).Path
$root = [System.IO.Path]::GetFullPath($RehearsalRoot)
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $root.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not [System.IO.Path]::GetFileName($root).StartsWith('Waterline-Phase8-Rehearsal', [System.StringComparison]::Ordinal)) {
    throw "RehearsalRoot must be an explicit Waterline-Phase8-Rehearsal directory under the system temp folder."
}

$existingUninstallKeys = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8AFEC410-4D36-45F6-A683-CE3DFE7731C8}_is1',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8AFEC410-4D36-45F6-A683-CE3DFE7731C8}_is1'
)
$registeredInstallations = @($existingUninstallKeys | Where-Object { Test-Path -LiteralPath $_ })
if ($registeredInstallations.Count -gt 0) {
    throw 'A Waterline installation is already registered. The isolated rehearsal will not replace it.'
}

$actualBaselineHash = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualBaselineHash -ne $ExpectedBaselineSha256.ToLowerInvariant()) {
    throw "The v2.0.1 rehearsal installer does not match its expected SHA-256 digest."
}

$appRoot = Join-Path $root 'App'
$stateRoot = Join-Path $root 'Profile'
[System.IO.Directory]::CreateDirectory($appRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($stateRoot) | Out-Null
$statePath = Join-Path $stateRoot 'state.json'
$legacyState = @'
{
  "settings": {
    "dailyGoalOz": 96,
    "reminderIntervalMinutes": 45,
    "workdayStart": "08:30:00",
    "workdayEnd": "18:00:00",
    "remindersEnabled": true,
    "soundsEnabled": false,
    "reminderDays": ["Monday", "Wednesday", "Friday"]
  },
  "drinks": [
    { "id": 101, "amountOz": 12, "at": "2026-09-08T09:15:00-05:00" },
    { "id": 102, "amountOz": 20, "at": "2026-09-08T13:40:00-05:00" }
  ],
  "lastNotificationAt": "2026-09-08T08:00:00-05:00"
}
'@
[System.IO.File]::WriteAllText($statePath, $legacyState, [System.Text.UTF8Encoding]::new($false))
$stateHash = (Get-FileHash -LiteralPath $statePath -Algorithm SHA256).Hash

function Invoke-Installer([string]$Path, [string[]]$Arguments) {
    $process = Start-Process -FilePath $Path -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) {
        $process.Kill($true)
        throw "Installer timed out: $([System.IO.Path]::GetFileName($Path))"
    }
    if ($process.ExitCode -ne 0) { throw "Installer exited with code $($process.ExitCode): $([System.IO.Path]::GetFileName($Path))" }
}

try {
    Invoke-Installer $baseline @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOICONS', "/DIR=$appRoot")
    $installedExe = Join-Path $appRoot 'Waterline.exe'
    if (-not (Test-Path -LiteralPath $installedExe)) { throw 'The v2.0.1 clean-install rehearsal did not install Waterline.exe.' }
    $baselineVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).FileVersion
    if (-not $baselineVersion.StartsWith('2.0.1.')) { throw "The baseline executable reports $baselineVersion instead of 2.0.1." }

    Invoke-Installer $candidate @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOICONS', "/DIR=$appRoot")
    $candidateVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).FileVersion
    if (-not $candidateVersion.StartsWith('2.1.0.')) { throw "The upgraded executable reports $candidateVersion instead of 2.1.0." }
    if ((Get-FileHash -LiteralPath $statePath -Algorithm SHA256).Hash -ne $stateHash) { throw 'The upgrade installer changed the existing state file.' }
    $backupPath = Join-Path $stateRoot 'upgrade-backups\state-before-2.1.0.json'
    if (-not (Test-Path -LiteralPath $backupPath)) { throw 'The upgrade installer did not create its state rollback copy.' }
    if ((Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -ne $stateHash) { throw 'The installer rollback copy does not match the pre-upgrade state.' }

    $uninstaller = Join-Path $appRoot 'unins000.exe'
    Invoke-Installer $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    if (Test-Path -LiteralPath $installedExe) { throw 'Uninstall left the application executable behind.' }
    if ((Get-FileHash -LiteralPath $statePath -Algorithm SHA256).Hash -ne $stateHash) { throw 'Uninstall changed or removed the user state file.' }
    if ((Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -ne $stateHash) { throw 'Uninstall changed or removed the rollback copy.' }

    Write-Output 'Clean install: v2.0.1 verified.'
    Write-Output 'Upgrade: v2.0.1 to v2.1.0 verified in place.'
    Write-Output 'Preservation: source state and installer rollback copy are byte-identical.'
    Write-Output 'Uninstall: application files removed; profile and rollback copy preserved.'
    Write-Output "Evidence root: $root"
} finally {
    $uninstaller = Join-Path $appRoot 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        try { Invoke-Installer $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') } catch { }
    }
}
