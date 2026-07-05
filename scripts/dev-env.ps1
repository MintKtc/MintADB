# MintADB dev environment — dot-source before build/run scripts.
# Usage: . .\scripts\dev-env.ps1

$ErrorActionPreference = 'Stop'
$script:MintAdbRoot = Split-Path $PSScriptRoot -Parent

$jdkCandidates = @(
    'C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot',
    'C:\Program Files\Microsoft\jdk-17*',
    'C:\Program Files\Android\Android Studio\jbr'
)

$resolvedJdk = $null
foreach ($candidate in $jdkCandidates) {
    $match = Get-Item $candidate -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match -and (Test-Path (Join-Path $match.FullName 'bin\java.exe'))) {
        $resolvedJdk = $match.FullName
        break
    }
}

if (-not $resolvedJdk) {
    throw 'JDK 17 not found. Install: winget install Microsoft.OpenJDK.17'
}

$androidSdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
if (-not (Test-Path $androidSdk)) {
    throw "Android SDK not found at $androidSdk"
}

$env:JAVA_HOME = $resolvedJdk
$env:ANDROID_HOME = $androidSdk
$env:ANDROID_SDK_ROOT = $androidSdk

$pathParts = @(
    (Join-Path $resolvedJdk 'bin'),
    (Join-Path $androidSdk 'platform-tools'),
    (Join-Path $androidSdk 'cmdline-tools\latest\bin'),
    (Join-Path $androidSdk 'emulator'),
    (Join-Path $androidSdk 'build-tools\35.0.0')
) | Where-Object { Test-Path $_ }

$env:Path = ($pathParts + $env:Path) -join ';'

$androidProject = Join-Path $script:MintAdbRoot 'src\MintADB.Android'
$localProps = Join-Path $androidProject 'local.properties'
if (-not (Test-Path $localProps)) {
    $sdkDir = ($androidSdk -replace '\\', '\\')
    "sdk.dir=$sdkDir" | Set-Content -Path $localProps -Encoding ASCII
}

Write-Host '[OK] MintADB dev environment ready' -ForegroundColor Green
Write-Host "     JAVA_HOME    = $env:JAVA_HOME"
Write-Host "     ANDROID_HOME = $env:ANDROID_HOME"
Write-Host "     Project root = $script:MintAdbRoot"