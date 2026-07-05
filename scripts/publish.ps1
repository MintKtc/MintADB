# MintADB publish script
# Run: powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile zip

param(
    [ValidateSet('portable', 'standalone', 'zip', 'installer')]
    [string]$Profile = 'standalone',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $Root 'src\MintADB.Wpf\MintADB.Wpf.csproj'
$Dist = Join-Path $Root 'dist'
$OutName = switch ($Profile) {
    'portable'   { 'MintADB-portable' }
    'standalone' { 'MintADB' }
    'zip'        { 'MintADB' }
    'installer'  { 'MintADB' }
}
$OutDir = Join-Path $Dist $OutName

Write-Host '== MintADB publish ==' -ForegroundColor Cyan
Write-Host "Profile : $Profile"
Write-Host "Runtime : $Runtime"
Write-Host "Output  : $OutDir"
Write-Host ''

$publishArgs = @(
    'publish', $Project,
    '-c', 'Release',
    '-r', $Runtime,
    '-o', $OutDir,
    "-p:Version=$Version"
)

switch ($Profile) {
    'portable' {
        $publishArgs += '--self-contained', 'false'
    }
    { $_ -in 'standalone', 'zip', 'installer' } {
        $publishArgs += '--self-contained', 'true'
        $publishArgs += '-p:PublishReadyToRun=true'
    }
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $OutDir 'MintADB.exe'
if (-not (Test-Path $exe)) {
    $legacy = Join-Path $OutDir 'MintADB.Wpf.exe'
    if (Test-Path $legacy) { Copy-Item $legacy $exe }
}
if (-not (Test-Path $exe)) {
    Write-Error "Missing MintADB.exe in $OutDir"
}
$targetExe = $exe

$sizeMb = [math]::Round((Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ''
Write-Host "[OK] Published $sizeMb megabytes" -ForegroundColor Green
Write-Host "     Run: $targetExe"

if ($Profile -eq 'zip') {
    $zipPath = Join-Path $Dist "MintADB-v$Version-$Runtime.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $OutDir -DestinationPath $zipPath -CompressionLevel Optimal
    $zipMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "[OK] ZIP $zipPath ($zipMb megabytes)" -ForegroundColor Green
}

Write-Host ''
Write-Host 'Notes:' -ForegroundColor Yellow
if ($Profile -eq 'portable') {
    Write-Host '  - Target PC needs .NET 8 Desktop Runtime'
    Write-Host '    https://dotnet.microsoft.com/download/dotnet/8.0'
} else {
    Write-Host '  - Self-contained: no .NET install, unzip and run MintADB.exe'
}
Write-Host '  - Keep PlatformTools, Drivers, Miui folders next to exe'
Write-Host '  - User data: Desktop\MintADB'

if ($Profile -eq 'installer') {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        Write-Host '[WARN] Inno Setup not found. Install from https://jrsoftware.org/isinfo.php' -ForegroundColor Yellow
        Write-Host '       Then run: iscc scripts\MintADB.iss'
    } else {
        $iss = Join-Path $Root 'scripts\MintADB.iss'
        & $iscc $iss
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $setup = Get-ChildItem (Join-Path $Dist 'MintADB-Setup-*.exe') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($setup) {
            $setupMb = [math]::Round($setup.Length / 1MB, 1)
            Write-Host "[OK] Installer $($setup.FullName) ($setupMb megabytes)" -ForegroundColor Green
        }
    }
}