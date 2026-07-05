# Run MintADB WPF in Debug (fast dev loop).
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\run-pc.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\run-pc.ps1 -BootstrapOnly

param(
    [switch]$BootstrapOnly,
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dev-env.ps1')

$project = Join-Path $script:MintAdbRoot 'src\MintADB.Wpf\MintADB.Wpf.csproj'

Write-Host '== MintADB PC dev run ==' -ForegroundColor Cyan

$adb = Get-Command adb -ErrorAction SilentlyContinue
if ($adb) {
    $devices = @(& adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\tdevice$' })
    if ($devices) {
        Write-Host "[OK] ADB device(s): $(($devices | ForEach-Object { ($_ -split "`t")[0] }) -join ', ')" -ForegroundColor Green
    } else {
        Write-Host '[WARN] No ADB device — connect phone for full PC features' -ForegroundColor Yellow
    }
} else {
    Write-Host '[WARN] adb not in PATH' -ForegroundColor Yellow
}

Push-Location $script:MintAdbRoot
try {
    if ($BootstrapOnly) {
        Write-Host 'Running bootstrap-only...' -ForegroundColor Cyan
        $config = if ($Release) { 'Release' } else { 'Debug' }
        & dotnet run --project $project -c $config -- --bootstrap-only
    } else {
        $config = if ($Release) { 'Release' } else { 'Debug' }
        Write-Host "Starting dotnet run ($config)..." -ForegroundColor Cyan
        & dotnet run --project $project -c $config
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}