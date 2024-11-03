param (
    [switch]$Install,
    [switch]$Uninstall,
    [string]$ServiceName = "AlitaApiService",
    [string]$DisplayName = "Alita API Service",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$servicePath = Join-Path $scriptPath "../../publish/$Configuration/AlitaApi.exe"
$logPath = Join-Path $env:ProgramData "AlitaApi/logs"

function Write-Log {
    param($Message)
    $logFile = Join-Path $logPath "service-install.log"
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File -Append -FilePath $logFile
    Write-Host $Message
}

function Ensure-AdminPrivileges {
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Log "Please run as Administrator"
        exit 1
    }
}

function Install-AlitaService {
    Write-Log "Beginning service installation..."
    
    # Create log directory
    if (-not (Test-Path $logPath)) {
        New-Item -ItemType Directory -Path $logPath -Force | Out-Null
    }

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Log "Service already exists. Removing..."
        Uninstall-AlitaService
    }
    
    Write-Log "Installing service from: $servicePath"
    New-Service -Name $ServiceName `
                -BinaryPathName $servicePath `
                -DisplayName $DisplayName `
                -StartupType Automatic `
                -Description "Alita API Service"

    Start-Service -Name $ServiceName
    Write-Log "Service installed and started successfully"
}

function Uninstall-AlitaService {
    Write-Log "Beginning service uninstallation..."
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Stop-Service -Name $ServiceName -Force
        Write-Log "Waiting for service to stop..."
        $service.WaitForStatus('Stopped', '00:00:30')
        sc.exe delete $ServiceName
        Write-Log "Service uninstalled successfully"
    } else {
        Write-Log "Service not found"
    }
}

# Main execution
Ensure-AdminPrivileges

if ($Install) {
    Install-AlitaService
} elseif ($Uninstall) {
    Uninstall-AlitaService
} else {
    Write-Log "Please specify -Install or -Uninstall"
}
