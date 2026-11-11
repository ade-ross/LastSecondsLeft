@echo off
set "RUN=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
taskkill /im LastSecondsLeft.exe /f >nul 2>&1
taskkill /im TimerToDeath.exe /f >nul 2>&1
reg delete "%RUN%" /v LastSecondsLeft /f >nul 2>&1
reg delete "%RUN%" /v TimerToDeath /f >nul 2>&1
rem give the process a moment to release the file
ping -n 2 127.0.0.1 >nul
if exist "%LOCALAPPDATA%\LastSecondsLeft" rmdir /s /q "%LOCALAPPDATA%\LastSecondsLeft"
if exist "%LOCALAPPDATA%\TimerToDeath" rmdir /s /q "%LOCALAPPDATA%\TimerToDeath"
echo.
echo  LastSecondsLeft removed.
echo.
pause
