@echo off
rem Installs (or updates) LastSecondsLeft for the current user. No admin needed.
rem Settings (style, start %, length, size) are changed from the tray icon: double-click it.

set "SRC=%~dp0"
set "DEST=%LOCALAPPDATA%\LastSecondsLeft"
set "OLD=%LOCALAPPDATA%\TimerToDeath"
set "RUN=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"

if not exist "%SRC%LastSecondsLeft.exe" call "%SRC%build.bat"
if not exist "%SRC%LastSecondsLeft.exe" goto fail

rem Stop running copies (this app, and the old version under its old name)
taskkill /im LastSecondsLeft.exe /f >nul 2>&1
taskkill /im TimerToDeath.exe /f >nul 2>&1
ping -n 2 127.0.0.1 >nul

if not exist "%DEST%" mkdir "%DEST%"
copy /y "%SRC%LastSecondsLeft.exe" "%DEST%\LastSecondsLeft.exe" >nul
if errorlevel 1 goto fail

rem Remove the old version (TimerToDeath), keeping its settings
if exist "%OLD%\settings.ini" if not exist "%DEST%\settings.ini" copy /y "%OLD%\settings.ini" "%DEST%\settings.ini" >nul
reg delete "%RUN%" /v TimerToDeath /f >nul 2>&1
if exist "%OLD%" rmdir /s /q "%OLD%"

rem Start automatically at login
reg add "%RUN%" /v LastSecondsLeft /t REG_SZ /d "\"%DEST%\LastSecondsLeft.exe\"" /f >nul
if errorlevel 1 goto fail

start "" "%DEST%\LastSecondsLeft.exe"

echo.
echo  LastSecondsLeft installed and running.
echo  - Logo in the tray (click ^^ near the clock).
echo  - Double-click it (or right-click ^> Settings) to pick the style, start %%, length and size.
echo  - It starts by itself every time you log in.
echo  - To remove it: run uninstall.bat
echo.
pause
exit /b 0

:fail
echo.
echo  Installation FAILED.
pause
exit /b 1
