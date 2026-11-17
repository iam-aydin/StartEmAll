@echo off
rem Builds the standalone exe, then wraps it in an installer. Output: installer\Output\StartEmAll-Setup.exe
rem Needs the .NET 8 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup)
setlocal
set "VER=%~1"
if "%VER%"=="" set "VER=1.0.0"

dotnet publish StartEmAll.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=%VER% -o dist || goto :fail

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo Inno Setup 6 not found. Install it with:  winget install JRSoftware.InnoSetup
  goto :fail
)
"%ISCC%" /DAppVersion=%VER% installer\StartEmAll.iss || goto :fail
echo.
echo Done: installer\Output\StartEmAll-Setup.exe
exit /b 0
:fail
echo Build failed.
exit /b 1
