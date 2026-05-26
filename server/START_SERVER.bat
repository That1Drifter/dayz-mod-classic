@echo off
REM Launches Arma 2 OA dedicated server with DayZ Mod Classic 1.0 + HiveEXT.
REM RUN THIS FROM YOUR ARMA 2 OA INSTALL DIR (after copying bundle in place).
REM Expects: arma2oaserver.exe in current directory.
setlocal
cd /d "%~dp0"
title DayZ Mod Classic 1.0.0 Server (port 2302)

if not exist arma2oaserver.exe (
    echo ERROR: arma2oaserver.exe not found in current directory.
    echo This batch must run from the Arma 2 OA install root, not the staging bundle.
    echo Copy the bundle contents into your Arma 2 OA install, then run this from there.
    pause
    exit /b 1
)

echo ============================================================
echo  DayZ Mod Classic 1.0.0 Server
echo  Mod chain: -mod=@dayzmodclassic;@hive
echo  Port:      2302 UDP
echo  Config:    cfgdayz\server.cfg
echo  Log:       cfgdayz\server_console.log + RPT in %%LOCALAPPDATA%%\ArmA 2 OA
echo ============================================================
echo.
echo Make sure MySQL is running (START_MYSQL.bat) before clients connect.
echo.

arma2oaserver.exe ^
    -config=cfgdayz\server.cfg ^
    -profiles=cfgdayz ^
    -name=server ^
    -mod=@dayzmodclassic;@hive ^
    -port=2302

endlocal
