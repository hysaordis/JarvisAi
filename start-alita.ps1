$ErrorActionPreference = "Stop"

Write-Host "Starting Alita Assistant..." -ForegroundColor Cyan

# Start backend
$backendJob = Start-Job -ScriptBlock {
    Set-Location "iJarvis"
    dotnet run
}

# Wait for backend to initialize
Start-Sleep -Seconds 5

# Start frontend
$frontendJob = Start-Job -ScriptBlock {
    Set-Location "voice-interface-app"
    npm run tauri dev
}

Write-Host "Services started. Press Ctrl+C to stop all services." -ForegroundColor Green

try {
    Wait-Job -Job $backendJob, $frontendJob
} finally {
    Stop-Job -Job $backendJob, $frontendJob
    Remove-Job -Job $backendJob, $frontendJob
}
