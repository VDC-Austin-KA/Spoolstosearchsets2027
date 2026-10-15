@echo off
setlocal
rem Installs Spool To Search Sets for Navisworks Manage/Simulate 2027 for the current user (no admin needed).
set "SRC=%~dp0SpoolToSearchSets.bundle"
set "DEST=%APPDATA%\Autodesk\ApplicationPlugins\SpoolToSearchSets.bundle"

if not exist "%SRC%\PackageContents.xml" (
  echo Extract the whole zip to a folder first, then run Install.cmd from there.
  pause & exit /b 1
)
tasklist /FI "IMAGENAME eq Roamer.exe" 2>nul | find /I "Roamer.exe" >nul && (
  echo Close Navisworks, then run Install.cmd again.
  pause & exit /b 1
)

if exist "%DEST%" rmdir /S /Q "%DEST%"
xcopy "%SRC%" "%DEST%\" /E /I /Y /Q >nul || (
  echo Install failed: could not copy to "%DEST%".
  pause & exit /b 1
)
rem Files extracted from a downloaded zip are blocked; .NET refuses to load a blocked DLL.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath $env:DEST -Recurse -File | Unblock-File"

echo.
echo Installed to "%DEST%".
echo Start Navisworks 2027; the Spool To Search Sets button is on the "Tool add-ins 1" tab.
pause
