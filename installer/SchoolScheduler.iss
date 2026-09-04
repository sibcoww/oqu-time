#define AppName "SchoolScheduler"
#define AppVersion "1.0.0"
#define AppPublisher "SchoolScheduler"
#define AppExeName "SchoolScheduler.exe"
#define PublishDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{A69CD637-78E9-4D62-9944-E66397046573}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=SchoolScheduler-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectory: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDirectory := ExpandConstant('{localappdata}\SchoolScheduler');
    if DirExists(DataDirectory) and
       (MsgBox('Удалить пользовательские данные и базу расписания?' + #13#10 +
         'Если выбрать «Нет», данные сохранятся для будущей установки.',
         mbConfirmation, MB_YESNO) = IDYES) then
      DelTree(DataDirectory, True, True, True);
  end;
end;
