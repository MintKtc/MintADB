# One-time setup for MintADB test environment (PC + Android).
# Usage: powershell -ExecutionPolicy Bypass -File scripts\setup-test-env.ps1

param(
    [switch]$WithEmulator
)

$ErrorActionPreference = 'Stop'

Write-Host '== MintADB test environment setup ==' -ForegroundColor Cyan

function Test-CommandExists([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# --- JDK ---
if (-not (Test-CommandExists 'java')) {
    Write-Host 'Installing OpenJDK 17...' -ForegroundColor Cyan
    winget install --id Microsoft.OpenJDK.17 -e --source winget --accept-package-agreements --accept-source-agreements --silent
}

. (Join-Path $PSScriptRoot 'dev-env.ps1')

# --- Android SDK packages ---
$sdkmanager = Join-Path $env:ANDROID_HOME 'cmdline-tools\latest\bin\sdkmanager.bat'
if (-not (Test-Path $sdkmanager)) {
    Write-Host 'Downloading Android cmdline-tools...' -ForegroundColor Cyan
    $zip = Join-Path $env:TEMP 'cmdline-tools.zip'
    Invoke-WebRequest -Uri 'https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip' -OutFile $zip -UseBasicParsing
    $dest = Join-Path $env:ANDROID_HOME 'cmdline-tools'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Expand-Archive -Path $zip -DestinationPath $dest -Force
    if (Test-Path (Join-Path $dest 'cmdline-tools')) {
        Move-Item (Join-Path $dest 'cmdline-tools') (Join-Path $dest 'latest') -Force
    }
}

$licDir = Join-Path $env:ANDROID_HOME 'licenses'
New-Item -ItemType Directory -Force -Path $licDir | Out-Null
@{
    'android-sdk-license'         = '24333f8a63b6825ea9c5514f83c2829b004d1fee'
    'android-sdk-preview-license' = '84831b9409646a918e30573bab4c9c91346d9abd'
} | ForEach-Object { $_.GetEnumerator() } | ForEach-Object {
    Set-Content -Path (Join-Path $licDir $_.Key) -Value $_.Value -Encoding ASCII
}

$packages = @(
    'platform-tools',
    'platforms;android-35',
    'build-tools;35.0.0'
)
if ($WithEmulator) {
    $packages += 'emulator', 'system-images;android-35;google_apis;x86_64'
}

Write-Host 'Installing SDK packages...' -ForegroundColor Cyan
& $sdkmanager --sdk_root=$env:ANDROID_HOME @packages

# --- Gradle wrapper ---
$androidDir = Join-Path $script:MintAdbRoot 'src\MintADB.Android'
if (-not (Test-Path (Join-Path $androidDir 'gradlew.bat'))) {
    Write-Host 'Generating Gradle wrapper...' -ForegroundColor Cyan
    $gradleZip = Join-Path $env:TEMP 'gradle-8.9-bin.zip'
    if (-not (Test-Path (Join-Path $env:TEMP 'gradle-8.9'))) {
        Invoke-WebRequest -Uri 'https://services.gradle.org/distributions/gradle-8.9-bin.zip' -OutFile $gradleZip -UseBasicParsing
        Expand-Archive -Path $gradleZip -DestinationPath $env:TEMP -Force
    }
    Push-Location $androidDir
    & (Join-Path $env:TEMP 'gradle-8.9\bin\gradle.bat') wrapper --gradle-version 8.9
    Pop-Location
}

if ($WithEmulator) {
    $avdName = 'MintADB_Test'
    $avdManager = Join-Path $env:ANDROID_HOME 'cmdline-tools\latest\bin\avdmanager.bat'
    $existing = & $avdManager list avd 2>&1 | Out-String
    if ($existing -notmatch $avdName) {
        Write-Host "Creating AVD $avdName..." -ForegroundColor Cyan
        echo no | & $avdManager create avd -n $avdName -k 'system-images;android-35;google_apis;x86_64' -d pixel_6
    }
}

# --- .NET ---
if (-not (Test-CommandExists 'dotnet')) {
    Write-Host 'Installing .NET 8 SDK...' -ForegroundColor Cyan
    winget install --id Microsoft.DotNet.SDK.8 -e --source winget --accept-package-agreements --accept-source-agreements --silent
}

Write-Host ''
Write-Host '[OK] Test environment ready' -ForegroundColor Green
Write-Host ''
Write-Host 'Quick start:' -ForegroundColor Yellow
Write-Host '  PC app   : powershell -ExecutionPolicy Bypass -File scripts\run-pc.ps1'
Write-Host '  Android  : powershell -ExecutionPolicy Bypass -File scripts\run-android.ps1'
if ($WithEmulator) {
    Write-Host "  Emulator : emulator -avd MintADB_Test"
}
Write-Host ''
Write-Host 'Connected devices:' -ForegroundColor Yellow
& adb devices