# MintADB PC — dev environment (WPF only)
# Usage: . .\scripts\dev-env.ps1

$ErrorActionPreference = 'Stop'
$script:MintAdbRoot = Split-Path $PSScriptRoot -Parent

Write-Host '[OK] MintADB PC dev environment ready' -ForegroundColor Green
Write-Host "     Project root = $script:MintAdbRoot"
Write-Host "     WPF project  = $(Join-Path $script:MintAdbRoot 'src\MintADB.Wpf')"
Write-Host "     APK project  = $(Join-Path $script:MintAdbRoot 'MintADB-Android') (riêng)"