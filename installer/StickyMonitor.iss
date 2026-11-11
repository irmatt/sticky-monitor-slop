; Sticky Monitor - Inno Setup installer
; Per-user install (no admin required), Start Menu shortcut, uninstaller entry.

#define MyAppName "Sticky Monitor"
#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#define MyAppPublisher "Sticky Monitor"
#define MyAppExeName "StickyMonitor.App.exe"

[Setup]
AppId={{56029fb9-dd62-49fa-975e-f31a006df3ca}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/irmatt/sticky-monitor-slop
AppSupportURL=https://github.com/irmatt/sticky-monitor-slop/issues
AppUpdatesURL=https://github.com/irmatt/sticky-monitor-slop/releases
DefaultDirName={localappdata}\Programs\StickyMonitor
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\packages
OutputBaseFilename=StickyMonitor-Setup-{#MyAppVersion}
SetupIconFile=..\assets\app-icon.ico
UninstallDisplayIcon={app}\app-icon.ico
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The app creates its own "launch at login" shortcut; Inno does not know about it.
Type: files; Name: "{userstartup}\Sticky Monitor.lnk"

[Code]
// Sticky Monitor is a tray app, so it is routinely running when someone installs an
// update. It does not register with the Restart Manager, so AppMutex + CloseApplications
// only produced a prompt that a silent install answered with "Cancel". Close it outright
// instead, both for install and for uninstall, so locked files can be replaced/removed.
procedure StopStickyMonitor();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM StickyMonitor.App.exe /F', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopStickyMonitor();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopStickyMonitor();
  end;
end;
