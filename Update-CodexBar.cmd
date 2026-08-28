@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Windows\Update-CodexBar.ps1"
set "updateExitCode=%ERRORLEVEL%"

echo.
if not "%updateExitCode%"=="0" (
    echo CodexBar update failed. Review the error above.
) else (
    echo CodexBar is up to date. You can close this window.
)
pause
exit /b %updateExitCode%
