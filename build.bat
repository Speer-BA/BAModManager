@echo off
setlocal
cd /d "%~dp0"

rem  build.bat  ->  Broken Arrow Mod Manager (BAModManager.exe) - the full manager, for modders
rem  For the players' Mod Launcher use build_launcher.bat

set OUT=publish\manager
echo Building Broken Arrow Mod Manager...
echo.

rem One standalone .exe - nobody needs .NET installed.
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish BAModManager.csproj -c Release -r win-x64 --self-contained true ^
  -p:AppFlavor=Manager ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo Build FAILED - see the errors above.
  pause
  exit /b 1
)

rem Release zip for GitHub: the exe, the few runtime DLLs next to it and the font license (required by the OFL)
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$v=(Get-Item '%OUT%\BAModManager.exe').VersionInfo; '{0}.{1}.{2}' -f $v.FileMajorPart,$v.FileMinorPart,$v.FileBuildPart"`) do set VERSION=%%v
if not exist release mkdir release
set ZIP=release\BAModManager-v%VERSION%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%'"

echo.
echo Done:  %~dp0%OUT%\BAModManager.exe
echo Release zip:  %~dp0%ZIP%
pause
