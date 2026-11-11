@echo off
rem Builds LastSecondsLeft.exe with the C# compiler that ships with Windows. No installs needed.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" echo ERROR: .NET Framework C# compiler not found. & exit /b 1

set "OUT=%~dp0LastSecondsLeft.exe"
set "ICO=%~dp0LastSecondsLeft.ico"
set "ARGS=/nologo /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll"

rem The logo is drawn in code: build once, let the exe write its own .ico, then build again with it.
if exist "%ICO%" goto withicon
"%CSC%" %ARGS% /out:"%OUT%" "%~dp0LastSecondsLeft.cs" || exit /b 1
start "" /wait "%OUT%" /writeicon:"%ICO%"
:withicon
"%CSC%" %ARGS% /win32icon:"%ICO%" /out:"%OUT%" "%~dp0LastSecondsLeft.cs" || exit /b 1
