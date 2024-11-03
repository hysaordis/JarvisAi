@echo off
echo Stopping Alita services...

:: Kill backend process
taskkill /FI "WINDOWTITLE eq Alita Backend*" /T /F

:: Kill frontend process
taskkill /FI "WINDOWTITLE eq Alita Frontend*" /T /F

echo Alita services stopped.
