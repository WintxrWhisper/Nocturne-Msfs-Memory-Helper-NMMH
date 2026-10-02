@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" exit /b 1
if not exist "%~dp0tests\bin" mkdir "%~dp0tests\bin"
"%CSC%" /nologo /target:exe /platform:x64 /optimize+ /debug- /reference:System.dll /reference:System.Core.dll /out:"%~dp0tests\bin\SimConnectProbeTests.exe" "%~dp0src\SimConnectProbe.cs" "%~dp0tests\SimConnectProbeTests.cs"
if errorlevel 1 exit /b 1
"%~dp0tests\bin\SimConnectProbeTests.exe"
exit /b %ERRORLEVEL%
