@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo.
    echo ERROR: The Windows .NET Framework C# compiler was not found.
    echo Install or enable .NET Framework 4.8, then run this file again.
    echo.
    pause
    exit /b 1
)

set "RELEASE_ROOT=%~dp0release"
set "RELEASE_DIR=%RELEASE_ROOT%\Nocturne-MSFS-Memory-Helper-1.5.4-RC"
set "OUTPUT_EXE=%RELEASE_DIR%\Nocturne-MSFS-Memory-Helper-1.5.4-RC.exe"
set "OUTPUT_ZIP=%RELEASE_ROOT%\Nocturne-MSFS-Memory-Helper-1.5.4-RC.zip"

if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
if exist "%OUTPUT_ZIP%" del /f /q "%OUTPUT_ZIP%"
mkdir "%RELEASE_DIR%"
if errorlevel 1 goto :failed

echo Building Nocturne MSFS Memory Helper 1.5.4 RC...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /debug- ^
    /win32icon:"%~dp0src\NMMH.ico" ^
    /win32manifest:"%~dp0src\NMMH.manifest" ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Runtime.Serialization.dll ^
    /reference:Microsoft.CSharp.dll ^
    /out:"%OUTPUT_EXE%" ^
    "%~dp0src\AssemblyInfo.cs" ^
    "%~dp0src\Program.cs" ^
    "%~dp0src\Storage.cs" ^
    "%~dp0src\NativeMemory.cs" ^
    "%~dp0src\SimConnectProbe.cs" ^
    "%~dp0src\CleanupWorker.cs" ^
    "%~dp0src\MsfsWatcher.cs" ^
    "%~dp0src\TaskSchedulerManager.cs" ^
    "%~dp0src\MainForm.cs"

if errorlevel 1 goto :failed
if not exist "%OUTPUT_EXE%" goto :failed

copy /y "%~dp0README.txt" "%RELEASE_DIR%\README.txt" >nul

where tar.exe >nul 2>nul
if errorlevel 1 (
    echo.
    echo Build succeeded. Windows tar.exe was not found, so the release folder was not zipped.
    echo Release folder:
    echo   %RELEASE_DIR%
    echo.
    pause
    exit /b 0
)

pushd "%RELEASE_ROOT%"
tar.exe -a -c -f "%OUTPUT_ZIP%" "Nocturne-MSFS-Memory-Helper-1.5.4-RC"
set "TAR_RESULT=%ERRORLEVEL%"
popd
if not "%TAR_RESULT%"=="0" goto :zipfailed

echo.
echo Build succeeded.
echo.
echo EXE:
echo   %OUTPUT_EXE%
echo.
echo Release ZIP:
echo   %OUTPUT_ZIP%
echo.
certutil -hashfile "%OUTPUT_EXE%" SHA256
echo.
pause
exit /b 0

:zipfailed
echo.
echo The EXE built successfully, but the ZIP could not be created.
echo Release folder:
echo   %RELEASE_DIR%
echo.
pause
exit /b 2

:failed
echo.
echo BUILD FAILED. Read the compiler message above.
echo.
pause
exit /b 1
