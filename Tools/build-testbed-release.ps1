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
    [string]$LogFile = "$env:TEMP\probe-testbed-release.log",
    [switch]$KeepVersionCode,

    # Signed and uploadable exactly like the participant build, but carrying
    # the debug map, the status text and the way out of the blind condition.
    # For running a session over internal testing, where a development APK
    # cannot be uploaded and the handset has no cable.
    [switch]$Experimenter
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
if ($KeepVersionCode) { $env:PROBE_KEEP_VERSION_CODE = '1' }

$method = if ($Experimenter) {
    'CityGenPedestrianProbe.Editor.AndroidTestbedBuild.BuildExperimenterFromCommandLine'
} else {
    'CityGenPedestrianProbe.Editor.AndroidTestbedBuild.BuildReleaseFromCommandLine'
}

try {
    $which = if ($Experimenter) { 'experimenter' } else { 'participant' }
    Write-Host "Building the $which bundle. This takes a while." -ForegroundColor Cyan

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
        $method,
        '-logFile', $LogFile) | Out-Null

    # Three hours, not forty-five minutes. A release build moves CityGen's
    # Resources folder out and back, and the two asset reimports that costs put
    # it near two hours where a development APK takes fifteen minutes.
    $deadline = (Get-Date).AddHours(3)
    $started = Get-Date
    $nextStatus = $started

    # Only the two lines our own build code writes when it is finished.
    # "Application will terminate" cannot be used and neither can "Exiting
    # batchmode": the launcher process prints the first of those seconds into
    # every run as it hands off to the editor it relaunches, so watching for it
    # abandons a healthy build twenty seconds in and calls it a failure.
    $terminal = 'Android build Succeeded|Android build Failed'
    $done = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        if ((Test-Path $LogFile) -and
            (Select-String -Path $LogFile -Pattern $terminal -Quiet)) {
            $done = $true
            break
        }

        # A build that dies without reaching either line leaves no editor
        # behind. Not trusted for the first minute, because the launcher exits
        # before the editor it relaunches has appeared.
        if (((Get-Date) - $started).TotalSeconds -gt 60 -and
            -not (Get-Process Unity -ErrorAction SilentlyContinue)) {
            Write-Host "Unity stopped without reporting a result." -ForegroundColor Yellow
            break
        }

        if (-not (Test-Path $LogFile)) { continue }
        if ((Get-Date) -ge $nextStatus) {
            $recent = Get-Content $LogFile -Tail 200
            $phase = if ($recent -match 'Gradle|bundleRelease') { 'Gradle packaging' }
                elseif ($recent -match 'clang\+\+|IL2CPP|bee_backend') { 'IL2CPP compile' }
                elseif ($recent -match 'Compiling shader') { 'Shader compile' }
                else { 'Unity initialization' }
            $elapsed = [math]::Floor(((Get-Date) - $started).TotalSeconds)
            Write-Host ("  [{0:mm\:ss}] {1}" -f [timespan]::FromSeconds($elapsed), $phase)
            $nextStatus = (Get-Date).AddSeconds(15)
        }
    }

    if (-not $done -and (Get-Date) -ge $deadline) {
        Write-Host "Gave up waiting after three hours." -ForegroundColor Yellow
    }
    $code = if ($done -and
        (Select-String -Path $LogFile -Pattern 'Android build Succeeded' -Quiet)) { 0 } else { 1 }
}
finally {
    # Cleared even if the build throws, so the password does not outlive it in
    # a shell the user keeps working in.
    $plain = $null
    Remove-Item Env:PROBE_KEYSTORE_PASS, Env:PROBE_KEYALIAS_PASS, Env:PROBE_KEEP_VERSION_CODE -ErrorAction SilentlyContinue
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

$name = if ($Experimenter) { 'PedestrianProbe-testbed-experimenter.aab' }
        else { 'PedestrianProbe-testbed.aab' }
$aab = Join-Path $project "Builds\Android\$name"
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
