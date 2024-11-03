param (
    [Parameter(Mandatory=$false)]
    [ValidateSet("publish", "install", "all")]
    [string]$Action = "all",

    [Parameter(Mandatory=$false)]
    [ValidateSet("win", "osx", "both")]
    [string]$Platform = "both",

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptPath ".."
$publishPath = Join-Path $projectPath "publish"

function Write-Log {
    param($Message)
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): $Message"
}

function Publish-Project {
    param (
        [string]$Runtime
    )
    
    Write-Log "Publishing for $Runtime..."
    $outputPath = Join-Path $publishPath $Runtime
    
    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        --output $outputPath `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true

    if ($LASTEXITCODE -ne 0) {
        throw "Publishing failed for $Runtime"
    }
    
    Write-Log "Published successfully to: $outputPath"
    return $outputPath
}

function Install-Service {
    param (
        [string]$Runtime,
        [string]$PublishPath
    )
    
    Write-Log "Installing service for $Runtime..."
    
    if ($Runtime -eq "win-x64") {
        $servicePath = Join-Path $projectPath "scripts/windows/install-service.ps1"
        & $servicePath -Install -Configuration $Configuration
    }
    elseif ($Runtime -eq "osx-x64") {
        $servicePath = Join-Path $projectPath "scripts/macos/install-service.sh"
        if ($IsWindows) {
            Write-Log "Cannot install macOS service from Windows"
            return
        }
        chmod +x $servicePath
        & $servicePath
    }
}

function Process-Platform {
    param (
        [string]$Runtime
    )
    
    if ($Action -eq "publish" -or $Action -eq "all") {
        $outputPath = Publish-Project -Runtime $Runtime
    }
    
    if ($Action -eq "install" -or $Action -eq "all") {
        Install-Service -Runtime $Runtime -PublishPath $outputPath
    }
}

# Create publish directory if it doesn't exist
if (-not (Test-Path $publishPath)) {
    New-Item -ItemType Directory -Path $publishPath | Out-Null
}

# Process requested platforms
try {
    if ($Platform -eq "win" -or $Platform -eq "both") {
        Process-Platform -Runtime "win-x64"
    }
    
    if ($Platform -eq "osx" -or $Platform -eq "both") {
        Process-Platform -Runtime "osx-x64"
    }
    
    Write-Log "Operations completed successfully!"
} catch {
    Write-Log "Error: $_"
    exit 1
}
