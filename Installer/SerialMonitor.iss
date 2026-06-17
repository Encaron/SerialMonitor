#define MyAppName "Serial Monitor"
#define MyAppVersion "2.2.0"
#define MyAppPublisher "Encaron"
#define MyAppURL "https://github.com/Encaron/SerialMonitor"
#define MyAppExeName "Serial Monitor.exe"
#define MySourceDir "E:\serial\Serial Monitor V2"
#define DotNetRuntime "dotnet-runtime-8.0.28-win-x64.exe"

[Setup]
AppId={{C8A7B3D6-1F4E-4A2D-9E5C-7B8A3F2D1E6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=SerialMonitor-Setup-v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#MySourceDir}\Icons\icon.ico

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: checkedonce
Name: "startmenu"; Description: "创建开始菜单文件夹"; GroupDescription: "附加图标:"; Flags: checkedonce

[Files]
; 主程序
Source: "{#MySourceDir}\Serial Monitor.exe"; DestDir: "{app}"; Flags: ignoreversion

; 图标素材（运行时文件系统加载）
Source: "{#MySourceDir}\Icons\README.md"; DestDir: "{app}\Icons"; Flags: ignoreversion
Source: "{#MySourceDir}\Icons\joystick\README.md"; DestDir: "{app}\Icons\joystick"; Flags: ignoreversion
Source: "{#MySourceDir}\Icons\joystick\pad_*.png"; DestDir: "{app}\Icons\joystick"; Flags: ignoreversion
Source: "{#MySourceDir}\Icons\joystick\thumb_*.png"; DestDir: "{app}\Icons\joystick"; Flags: ignoreversion
Source: "{#MySourceDir}\Icons\sliders\README.md"; DestDir: "{app}\Icons\sliders"; Flags: ignoreversion
Source: "{#MySourceDir}\Icons\sliders\thumb_*.png"; DestDir: "{app}\Icons\sliders"; Flags: ignoreversion

; .NET 8 运行时安装包（临时解压到 {tmp}，安装后删除）
Source: "{#DotNetRuntime}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsDotNet8Installed

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenu
Name: "{autoprograms}\{#MyAppName}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; Tasks: startmenu
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 如果没装 .NET 8，先静默安装运行时
Filename: "{tmp}\{#DotNetRuntime}"; Parameters: "/quiet /norestart"; Check: not IsDotNet8Installed; StatusMsg: "正在安装 .NET 8 运行环境..."; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// 检测 .NET 8 运行时是否已安装
function IsDotNet8Installed: Boolean;
var
  DotNetDir: String;
  FindRec: TFindRec;
begin
  Result := False;

  // 检查 shared 目录是否存在 8.0.x 版本
  DotNetDir := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.NETCore.App');
  if DirExists(DotNetDir) then
  begin
    if FindFirst(DotNetDir + '\8.0.*', FindRec) then
    begin
      Result := True;
      FindClose(FindRec);
    end;
  end;

  // 64位系统上 dotnet 可能在 Program Files（不是 x86）
  if not Result then
  begin
    DotNetDir := 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App';
    if DirExists(DotNetDir) then
    begin
      if FindFirst(DotNetDir + '\8.0.*', FindRec) then
      begin
        Result := True;
        FindClose(FindRec);
      end;
    end;
  end;
end;
