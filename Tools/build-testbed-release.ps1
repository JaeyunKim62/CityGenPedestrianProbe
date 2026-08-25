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
    Write-Host "Building. This takes about thirteen minutes." -ForegroundColor Cyan
    & $Unity -batchmode -quit `
        -projectPath $project `
        -buildTarget Android `
        -executeMethod CityGenPedestrianProbe.Editor.AndroidTestbedBuild.BuildReleaseFromCommandLine `
        -logFile $LogFile
    $code = $LASTEXITCODE
}
finally {
    # Cleared even if the build throws, so the password does not outlive it in
    # a shell the user keeps working in.
    $plain = $null
    Remove-Item Env:PROBE_KEYSTORE_PASS, Env:PROBE_KEYALIAS_PASS -ErrorAction SilentlyContinue
}

Select-String -Path $LogFile -Pattern 'Signing with|Version code|Android build |error CS' |
    ForEach-Object { $_.Line.Trim() }

$aab = Join-Path $project 'Builds\Android\PedestrianProbe-testbed.aab'
if ($code -eq 0 -and (Test-Path $aab)) {
    $mb = [math]::Round((Get-Item $aab).Length / 1MB, 1)
    Write-Host "`n$aab  ($mb MB)" -ForegroundColor Green
    Write-Host "Upload this to the Play Console internal testing track."
}
else {
    Write-Host "`nBuild failed. Full log: $LogFile" -ForegroundColor Red
    Write-Host "If it stopped partway, run Tools > Pedestrian Probe > Restore CityGen Resources in the editor."
    exit 1
}
