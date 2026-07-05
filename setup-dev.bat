@echo off
title MintADB - Kiem tra moi truong dev
echo ========================================
echo   MintADB - Kiem tra moi truong C# WPF
echo ========================================
echo.

set "PATH=%PATH%;C:\Program Files\dotnet;C:\Program Files\Git\cmd;C:\Users\%USERNAME%\AppData\Local\Programs\Microsoft VS Code\bin"

echo [1/5] .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo   FAIL - Chua cai .NET SDK
) else (
    for /f %%i in ('dotnet --version') do echo   OK   - .NET %%i
)

echo [2/5] Git
git --version >nul 2>&1
if %errorlevel% neq 0 (
    echo   FAIL - Chua cai Git
) else (
    for /f "tokens=1-3" %%a in ('git --version') do echo   OK   - Git %%c
)

echo [3/5] VS Code
code --version >nul 2>&1
if %errorlevel% neq 0 (
    echo   FAIL - Chua cai VS Code
) else (
    for /f %%i in ('code --version') do echo   OK   - VS Code %%i & goto :vscode_ok
    :vscode_ok
)

echo [4/5] Visual Studio 2022
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" (
    echo   OK   - VS 2022 Community
) else (
    echo   ...  - Dang cai hoac chua xong
)

echo [5/5] Build MintADB.Wpf
cd /d "%~dp0src\MintADB.Wpf"
dotnet build -c Release >nul 2>&1
if %errorlevel% neq 0 (
    echo   FAIL - Build loi
) else (
    echo   OK   - Build thanh cong
)

echo.
echo ========================================
echo   Mo project:
echo   - VS Code:  code "%~dp0src\MintADB.Wpf"
echo   - VS 2022:  devenv "%~dp0MintADB.sln"
echo   - Chay app: run-wpf.bat
echo ========================================
pause