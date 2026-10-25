@echo off
setlocal
cd /d "%~dp0"

rem  build_launcher.bat                      ->  Mod Launcher with no modpack; a modder sets one up
rem                                              with "Set up modpack (for modders)..." (saved in launcher.json)
rem  build_launcher.bat owner/repo           ->  Mod Launcher with that modpack built in (no launcher.json needed)
rem  build_launcher.bat owner/repo owner/repo2  ->  the same, plus a "Testing" channel players can switch to
rem  e.g. build_launcher.bat Speer-BA/ActualArrow Speer-BA/ActualArrow-Testing
set PACK=%~1
set TESTING=%~2
set OUT=publish\launcher
set NAME=BAModLauncher
if not "%PACK%"=="" (
  for /f "tokens=2 delims=/" %%r in ("%PACK%") do set NAME=%%r-Launcher
  set OUT=publish\launcher-%PACK:/=-%
)

echo Building Broken Arrow Mod Launcher %PACK% %TESTING%...
echo.

if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish BAModManager.csproj -c Release -r win-x64 --self-contained true ^
  -p:AppFlavor=Launcher ^
  "-p:DefaultModPack=%PACK%" ^
  "-p:TestingModPack=%TESTING%" ^
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

for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$v=(Get-Item '%OUT%\BAModLauncher.exe').VersionInfo; '{0}.{1}.{2}' -f $v.FileMajorPart,$v.FileMinorPart,$v.FileBuildPart"`) do set VERSION=%%v
if not exist release mkdir release
set ZIP=release\%NAME%-v%VERSION%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%'"

echo.
echo Done:  %~dp0%OUT%\BAModLauncher.exe
echo Release zip:  %~dp0%ZIP%
pause
