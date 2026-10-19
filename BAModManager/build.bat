@echo off
setlocal
cd /d "%~dp0"

echo Building Broken Arrow Mod Manager...
echo.

rem One standalone .exe - players don't need .NET installed.
if exist publish rmdir /s /q publish
dotnet publish BAModManager.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o publish

if errorlevel 1 (
  echo.
  echo Build FAILED - see the errors above.
  pause
  exit /b 1
)

rem Release zip for GitHub: the exe + the font license (required by the OFL)
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "([xml](Get-Content 'BAModManager.csproj')).Project.PropertyGroup.Version"`) do set VERSION=%%v
if not exist release mkdir release
set ZIP=release\BAModManager-v%VERSION%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path 'publish\BAModManager.exe','publish\Inter-LICENSE.txt' -DestinationPath '%ZIP%'"

echo.
echo Done:  %~dp0publish\BAModManager.exe
echo Release zip:  %~dp0%ZIP%
pause
