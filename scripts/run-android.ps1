# Build, install and launch MintADB Android on a connected device.
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\run-android.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\run-android.ps1 -SkipBuild
#   powershell -ExecutionPolicy Bypass -File scripts\run-android.ps1 -Logcat

param(
    [switch]$SkipBuild,
    [switch]$Logcat,
    [string]$DeviceId = '',
    [ValidateSet('debug', 'release')]
    [string]$Variant = 'debug'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dev-env.ps1')

$androidDir = Join-Path $script:MintAdbRoot 'src\MintADB.Android'
$package = 'com.minthd.mintadb'
$activity = "$package/.MainActivity"
$apkName = if ($Variant -eq 'release') { 'app-release.apk' } else { 'app-debug.apk' }
$apkPath = Join-Path $androidDir "app\build\outputs\apk\$Variant\$apkName"
$distApk = Join-Path $script:MintAdbRoot "dist\MintADB-v1.0.0-android-$Variant.apk"

function Get-TargetDevice {
    param([string]$PreferredId)
    $lines = @(& adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\tdevice$' })
    if ($lines.Count -eq 0) {
        throw 'No Android device connected. Enable USB debugging and reconnect.'
    }
    if ($PreferredId) {
        $match = $lines | Where-Object { $_ -match "^$([regex]::Escape($PreferredId))\t" }
        if (-not $match) { throw "Device $PreferredId not found." }
        return $PreferredId
    }
    return ($lines[0] -split "`t")[0]
}

function Show-DeviceInfo {
    param([string]$Id)
    $model = (& adb -s $Id shell getprop ro.product.model).Trim()
    $android = (& adb -s $Id shell getprop ro.build.version.release).Trim()
    $shizuku = (& adb -s $Id shell pm list packages moe.shizuku 2>$null) -match 'shizuku'
    Write-Host ''
    Write-Host "Device : $Id" -ForegroundColor Cyan
    Write-Host "Model  : $model (Android $android)"
    if ($shizuku) {
        Write-Host 'Shizuku: installed' -ForegroundColor Green
    } else {
        Write-Host 'Shizuku: NOT installed — install Shizuku before using advanced tools' -ForegroundColor Yellow
    }
}

Write-Host '== MintADB Android test run ==' -ForegroundColor Cyan

$device = Get-TargetDevice -PreferredId $DeviceId
Show-DeviceInfo -Id $device

if (-not $SkipBuild) {
    Write-Host ''
    Write-Host 'Building APK...' -ForegroundColor Cyan
    Push-Location $androidDir
    try {
        $task = if ($Variant -eq 'release') { 'assembleRelease' } else { 'assembleDebug' }
        & .\gradlew.bat $task --no-daemon
        if ($LASTEXITCODE -ne 0) { throw "Gradle $task failed with exit code $LASTEXITCODE" }
    } finally {
        Pop-Location
    }
}

if (-not (Test-Path $apkPath)) {
    throw "APK not found: $apkPath (run without -SkipBuild first)"
}

$distDir = Split-Path $distApk -Parent
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
Copy-Item $apkPath $distApk -Force
$apkMb = [math]::Round((Get-Item $apkPath).Length / 1MB, 2)

Write-Host ''
Write-Host "Installing $apkMb MB APK..." -ForegroundColor Cyan
& adb -s $device install -r $apkPath
if ($LASTEXITCODE -ne 0) { throw 'adb install failed' }

Write-Host 'Launching MintADB...' -ForegroundColor Cyan
& adb -s $device shell am start -n $activity
if ($LASTEXITCODE -ne 0) { throw 'Failed to launch app' }

Write-Host ''
Write-Host '[OK] MintADB running on device' -ForegroundColor Green
Write-Host "     APK : $distApk"
Write-Host '     Tip : Grant Shizuku permission inside the app, then tap Refresh'

if ($Logcat) {
    Write-Host ''
    Write-Host 'Logcat (Ctrl+C to stop):' -ForegroundColor Cyan
    & adb -s $device logcat --pid=$(adb -s $device shell pidof -s $package) 2>$null
    if ($LASTEXITCODE -ne 0) {
        & adb -s $device logcat -s MintADB:V ShellUserService:I AndroidRuntime:E
    }
}