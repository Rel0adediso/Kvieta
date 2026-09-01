@echo off
setlocal EnableExtensions

if /I "%~1"=="--help" (
  echo Kvieta Alpha 1 Hotfix 1 - Safe Mode recovery helper
  echo Place this file beside Kvieta-Setup-Alpha-1-Hotfix-1.exe and run as administrator in Safe Mode.
  exit /b 0
)

fltmc >nul 2>&1
if errorlevel 1 (
  echo Requesting administrator permission...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "SETUP=%~dp0Kvieta-Setup-Alpha-1-Hotfix-1.exe"
if not exist "%SETUP%" (
  echo.
  echo Kvieta-Setup-Alpha-1-Hotfix-1.exe was not found beside this recovery file.
  echo Put both files in the same folder and run this file again.
  echo.
  pause
  exit /b 2
)

echo Enabling Windows Installer temporarily in Safe Mode...
reg.exe add "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\MSIServer" /ve /t REG_SZ /d Service /f >nul
reg.exe add "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\MSIServer" /ve /t REG_SZ /d Service /f >nul
net.exe start msiserver >nul 2>&1
if errorlevel 1 (
  echo Windows Installer could not be started.
  pause
  exit /b 3
)

echo Starting Kvieta Hotfix Setup...
echo Keep the existing settings and complete the repair/update flow.
echo A Guardian-start warning in Safe Mode is expected; close Setup after it appears.
start "" /wait "%SETUP%"
set "SETUP_EXIT=%ERRORLEVEL%"

echo Cleaning the temporary Safe Mode installer setting...
net.exe stop msiserver >nul 2>&1
reg.exe delete "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\MSIServer" /f >nul 2>&1
reg.exe delete "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\MSIServer" /f >nul 2>&1

echo.
echo Setup closed with code %SETUP_EXIT%.
echo Restart Windows normally. Guardian will start with the hotfix binary.
echo.
choice.exe /C YN /M "Restart now"
if errorlevel 2 exit /b %SETUP_EXIT%
shutdown.exe /r /t 0
