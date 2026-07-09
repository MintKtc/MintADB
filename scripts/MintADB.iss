; MintADB Windows Installer (Inno Setup 6+)
; Build: iscc scripts\MintADB.iss
; Requires: dist\MintADB\ from publish.ps1 first

#define AppName "MintADB"
#define AppVersion "1.0.2"
#define AppPublisher "MINT_HD"
#define AppExe "MintADB.exe"
#define PublishDir "..\dist\MintADB"
#define AppIcon "..\exe.ico"

[Setup]
AppId={{8F4E2A1B-9C3D-4E5F-A6B7-1C2D3E4F5A6B}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/MintKtc/MintADB
AppSupportURL=https://github.com/MintKtc/MintADB/issues
AppVerName={#AppName} {#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=MintADB — ADB tool for Xiaomi / HyperOS by MINT_HD
VersionInfoCopyright=Copyright (C) 2026 MINT_HD
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
; Cài mặc định: C:\Program Files\MintADB
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Build ra dist\ — publish.ps1 copy sang release\
OutputDir=..\dist
OutputBaseFilename=MintADB-Setup-v{#AppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
; Close adb/scrcpy/MintADB holding files during upgrade
CloseApplications=yes
RestartApplications=no
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#AppExe}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "installusbdriver"; Description: "Install Google USB driver (android_winusb) after setup"; GroupDescription: "Optional:"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Tu dong trien khai ADB/Fastboot + thu muc du lieu (khong hien UI chinh)
Filename: "{app}\{#AppExe}"; Parameters: "--bootstrap-only"; StatusMsg: "Configuring ADB/Fastboot..."; Flags: waituntilterminated

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('installusbdriver') then
  begin
    if FileExists(ExpandConstant('{app}\Drivers\usb_driver\android_winusb.inf')) then
    begin
      if MsgBox('Install Google USB driver (requires Admin)?' + #13#10 + #13#10 +
        'You can also install later in the app: Tools > Basic > Install USB driver.',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        Exec('pnputil.exe', '/add-driver "' + ExpandConstant('{app}\Drivers\usb_driver\android_winusb.inf') + '" /install',
          '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      end;
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\MintADB\platform-tools"
Type: files; Name: "{localappdata}\MintADB\install-state.json"