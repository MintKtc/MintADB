# Redirect — APK project đã tách sang MintADB-Android/
# Usage: powershell -ExecutionPolicy Bypass -File scripts\run-android.ps1

param(
    [switch]$SkipBuild,
    [switch]$Logcat,
    [string]$DeviceId = '',
    [ValidateSet('debug', 'release')]
    [string]$Variant = 'debug'
)

$androidScript = Join-Path (Split-Path $PSScriptRoot -Parent) 'MintADB-Android\scripts\run.ps1'
if (-not (Test-Path $androidScript)) {
    throw "Không tìm thấy $androidScript — mở thư mục MintADB-Android/"
}

$argsList = @('-ExecutionPolicy', 'Bypass', '-File', $androidScript)
if ($SkipBuild) { $argsList += '-SkipBuild' }
if ($Logcat) { $argsList += '-Logcat' }
if ($DeviceId) { $argsList += '-DeviceId'; $argsList += $DeviceId }
if ($Variant -ne 'debug') { $argsList += '-Variant'; $argsList += $Variant }

& powershell @argsList
exit $LASTEXITCODE