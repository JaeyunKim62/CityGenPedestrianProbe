<#
.SYNOPSIS
Builds the signed testbed app bundle for Google Play.

.DESCRIPTION
Asks for the keystore password rather than taking it as an argument or reading
it from a file. Typing a password on a PowerShell command line writes it into
the PSReadLine history in plain text, where it stays; a prompt does not, and
neither does this script.

The password reaches Unity through the environment of this process only, so it
is gone when the script ends.

.EXAMPLE
.\Tools\build-testbed-release.ps1
#>

[CmdletBinding()]
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe",
    [string]$Keystore = "$env:USERPROFILE\pedestrianprobe-release.keystore",
    [string]$LogFile = "$env:TEMP\probe-testbed-release.log"
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Unity)) { throw "Unity not found: $Unity" }
if (-not (Test-Path $Keystore)) { throw "Keystore not found: $Keystore" }

$secure = Read-Host "Keystore password" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
if ([string]::IsNullOrWhiteSpace($plain)) { throw "No password given." }

$env:PROBE_KEYSTORE = $Keystore
$env:PROBE_KEYSTORE_PASS = $plain
$env:PROBE_KEYALIAS_PASS = $plain

try {
    Write-Host "Building. This takes a few minutes." -ForegroundColor Cyan

    # Removed first so that a build which never starts cannot be reported from
    # the previous run's log, which is exactly what happened twice: the script
    # announced a version code and a duration belonging to an earlier build.
    Remove-Item $LogFile -ErrorAction SilentlyContinue

    # Unity.exe relaunches itself and the process we start exits immediately,
    # so neither the call operator nor Start-Process -Wait waits for the build.
    # The log is the only honest signal: Unity writes a terminal line to it
    # whichever way the build ends.
    Start-Process -FilePath $Unity -PassThru -ArgumentList @(
        '-batchmode', '-quit',
        '-projectPath', $project,
        '-buildTarget', 'Android',
        '-executeMethod',
        'CityGenPedestrianProbe.Editor.AndroidTestbedBuild.BuildReleaseFromCommandLine',
        '-logFile', $LogFile) | Out-Null

    $deadline = (Get-Date).AddMinutes(45)
    $terminal = 'Exiting batchmode|Aborting batchmode|error CS\d|Android build Failed'
    $done = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        if (-not (Test-Path $LogFile)) { continue }
        if (Select-String -Path $LogFile -Pattern $terminal -Quiet) { $done = $true; break }
    }

    if (-not $done) { Write-Host "Gave up waiting after 45 minutes." -ForegroundColor Yellow }
    $code = if ($done -and
        (Select-String -Path $LogFile -Pattern 'Android build Succeeded' -Quiet)) { 0 } else { 1 }
}
finally {
    # Cleared even if the build throws, so the password does not outlive it in
    # a shell the user keeps working in.
    $plain = $null
    Remove-Item Env:PROBE_KEYSTORE_PASS, Env:PROBE_KEYALIAS_PASS -ErrorAction SilentlyContinue
}

if (Test-Path $LogFile) {
    Select-String -Path $LogFile -Pattern 'Signing with|Version code|Android build |error CS' |
        ForEach-Object { $_.Line.Trim() }
}
else {
    Write-Host "Unity wrote no log at $LogFile, so it never started. Check that " `
        "the editor is not already open with this project." -ForegroundColor Yellow
}

# Checked rather than mentioned in passing: a build that dies while the folder
# is parked leaves the editor unable to generate a city, and the next thing the
# user does is unlikely to be reading this script's last line.
$parked = Join-Path $project 'Assets\CityGen3D\Resources~Build'
if (Test-Path $parked) {
    Write-Host "`nCityGen's Resources folder is still parked at:" -ForegroundColor Yellow
    Write-Host "  $parked"
    Write-Host "Generating a city will not work until it is back. Either run"
    Write-Host "Tools > Pedestrian Probe > Restore CityGen Resources in the editor,"
    Write-Host "or rename that folder to Resources yourself."
}

$aab = Join-Path $project 'Builds\Android\PedestrianProbe-testbed.aab'
if ($code -eq 0 -and (Test-Path $aab)) {
    $item = Get-Item $aab
    $mb = [math]::Round($item.Length / 1MB, 1)
    Write-Host "`n$aab" -ForegroundColor Green
    Write-Host "  $mb MB, built $($item.LastWriteTime.ToString('HH:mm'))"
    Write-Host "  Upload this to the Play Console internal testing track."
}
else {
    Write-Host "`nBuild failed. Full log: $LogFile" -ForegroundColor Red
    exit 1
}
