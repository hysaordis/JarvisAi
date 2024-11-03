@echo off
echo Starting Alita Assistant...

:: Start backend (in a new window)
start "Alita Backend" cmd /k "cd iJarvis && dotnet run"

:: Wait for backend to initialize (adjust time if needed)
timeout /t 5 /nobreak

:: Start frontend (in a new window)
start "Alita Frontend" cmd /k "cd voice-interface-app && npm run tauri dev"

echo Alita services are starting...
