@echo off
REM Applies schema-patch.sql to hivemind DB. Run ONCE after first MySQL boot.
REM Idempotent: safe to re-run. DROP INDEX errors on first run are expected.
setlocal
cd /d "%~dp0"
echo Applying schema-patch.sql to hivemind...
"%~dp0MySQL\bin\mysql.exe" -h 127.0.0.1 -P 3316 -u root -proot --force hivemind < "%~dp0schema-patch.sql"
if errorlevel 1 (
    echo.
    echo WARN: mysql.exe returned non-zero. Some DROP INDEX statements may have
    echo       failed because the index didn't exist yet - that is harmless.
    echo       Verify column widths manually:
    echo         MySQL\bin\mysql.exe -h 127.0.0.1 -P 3316 -u root -proot hivemind -e "DESC character_data;"
) else (
    echo Schema patch applied cleanly.
)
endlocal
pause
