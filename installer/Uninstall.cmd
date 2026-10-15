@echo off
setlocal
set "DEST=%APPDATA%\Autodesk\ApplicationPlugins\SpoolToSearchSets.bundle"
tasklist /FI "IMAGENAME eq Roamer.exe" 2>nul | find /I "Roamer.exe" >nul && (
  echo Close Navisworks, then run Uninstall.cmd again.
  pause & exit /b 1
)
if exist "%DEST%" (rmdir /S /Q "%DEST%" & echo Removed "%DEST%".) else (echo Not installed.)
pause
