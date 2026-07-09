# MintADB publish script
# Run: powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile zip

param(
    [ValidateSet('portable', 'standalone', 'zip', 'installer')]
    [string]$Profile = 'standalone',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.2'
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

function Ensure-Scrcpy {
    param(
        [Parameter(Mandatory)][string]$TargetDir,
        [string]$SourceRoot = $Root
    )
    $dest = Join-Path $TargetDir 'scrcpy'
    $exe = Join-Path $dest 'scrcpy.exe'
    if (Test-Path $exe) {
        Write-Host "[OK] scrcpy present: $exe" -ForegroundColor Green
        return $true
    }

    $srcExe = Join-Path $SourceRoot 'src\MintADB.Wpf\PlatformTools\scrcpy\scrcpy.exe'
    if (Test-Path $srcExe) {
        $srcDir = Split-Path $srcExe -Parent
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Copy-Item (Join-Path $srcDir '*') $dest -Recurse -Force
        Write-Host "[OK] Copied scrcpy from source PlatformTools -> $dest" -ForegroundColor Green
        return (Test-Path $exe)
    }

    $scrcpyVersion = '4.0'
    $zipName = "scrcpy-win64-v$scrcpyVersion.zip"
    $zipUrl = "https://github.com/Genymobile/scrcpy/releases/download/v$scrcpyVersion/$zipName"
    $tmpZip = Join-Path $env:TEMP $zipName
    $tmpExtract = Join-Path $env:TEMP "scrcpy-publish-$scrcpyVersion"
    try {
        Write-Host "Downloading scrcpy v$scrcpyVersion ..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $zipUrl -OutFile $tmpZip -UseBasicParsing
        if (Test-Path $tmpExtract) { Remove-Item $tmpExtract -Recurse -Force }
        Expand-Archive -Path $tmpZip -DestinationPath $tmpExtract -Force
        $inner = Get-ChildItem $tmpExtract -Directory | Select-Object -First 1
        $innerPath = if ($inner) { $inner.FullName } else { $tmpExtract }
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Copy-Item (Join-Path $innerPath '*') $dest -Recurse -Force
        $srcKeep = Join-Path $SourceRoot 'src\MintADB.Wpf\PlatformTools\scrcpy'
        New-Item -ItemType Directory -Path $srcKeep -Force | Out-Null
        Copy-Item (Join-Path $innerPath '*') $srcKeep -Recurse -Force
        Write-Host "[OK] Bundled scrcpy into $dest" -ForegroundColor Green
        return (Test-Path $exe)
    } catch {
        Write-Host "[WARN] Could not download scrcpy: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host '       Screen mirror will fail until scrcpy is installed (see docs/BUNDLED-ASSETS.md)' -ForegroundColor Yellow
        return $false
    }
}

# Bundle scrcpy into source tree so dotnet publish CopyToOutputDirectory includes it
$srcPlatformTools = Join-Path $Root 'src\MintADB.Wpf\PlatformTools'
if (-not (Ensure-Scrcpy -TargetDir $srcPlatformTools)) {
    if ($Profile -eq 'installer') {
        Write-Error 'scrcpy is required for the installer package. Fix network or place scrcpy under PlatformTools\scrcpy\'
    }
}

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

# Double-check publish output (in case copy rules skipped nested files)
$outPlatformTools = Join-Path $OutDir 'PlatformTools'
if (-not (Ensure-Scrcpy -TargetDir $outPlatformTools)) {
    if ($Profile -eq 'installer') {
        Write-Error "scrcpy missing from publish output: $outPlatformTools\scrcpy\scrcpy.exe"
    }
}

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
Write-Host '  - Keep PlatformTools (adb + scrcpy), Drivers, Miui folders next to exe'
Write-Host '  - scrcpy: PlatformTools\scrcpy\scrcpy.exe (auto-downloaded if missing)'
Write-Host '  - User data: Desktop\MintADB'

if ($Profile -eq 'installer') {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    # Thư mục mặc định giao installer cho user / upload release
    $ReleaseDir = Join-Path $Root 'release'
    if (-not (Test-Path $ReleaseDir)) {
        New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
    }

    if (-not $iscc) {
        Write-Host '[WARN] Inno Setup not found. Install from https://jrsoftware.org/isinfo.php' -ForegroundColor Yellow
        Write-Host '       Then run: iscc scripts\MintADB.iss'
    } else {
        $iss = Join-Path $Root 'scripts\MintADB.iss'
        & $iscc $iss
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $setup = Get-ChildItem (Join-Path $Dist 'MintADB-Setup-*.exe') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($setup) {
            $dest = Join-Path $ReleaseDir $setup.Name
            Copy-Item $setup.FullName $dest -Force
            $setupMb = [math]::Round($setup.Length / 1MB, 1)
            Write-Host "[OK] Installer $($setup.FullName) ($setupMb megabytes)" -ForegroundColor Green
            Write-Host "[OK] Default folder: $dest" -ForegroundColor Green
        }
    }
}