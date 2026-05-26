@echo off
REM Starts portable MySQL 5.5.24 on port 3316. Leave window open while server runs.
setlocal
cd /d "%~dp0MySQL"
title DayZ 1.6 MySQL (port 3316)
if not exist dayz_log mkdir dayz_log
echo ============================================================
echo  DayZ 1.6 Hivemind DB - portable MySQL 5.5.24
echo  Port: 3316  User: root  Password: root  DB: hivemind
echo  Press Ctrl+C in this window to shut down.
echo ============================================================
.\bin\mysqld.exe --console
endlocal
