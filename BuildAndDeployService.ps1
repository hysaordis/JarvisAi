param (
    [Parameter(Mandatory=$false)]
    [ValidateSet("publish", "install", "uninstall", "all", "dev-ijarvis", "dev-visual", "dev-both")]  # Add "dev-both"
    [string]$Action = "all",

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Fix project path resolution
$projectPath = Join-Path $scriptPath "iJarvis"
$publishPath = Join-Path $scriptPath "publish"
$appSettingsSource = Join-Path $projectPath "appsettings.json"  # Add this line

$serviceName = "iJarvis"
$serviceDisplayName = "iJarvis Service"
$serviceDescription = "iJarvis Windows Service"

function Write-Log {
    param($Message)
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): $Message"
}

function Ensure-AdminPrivileges {
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Log "Please run as Administrator"
        exit 1
    }
}

function Setup-ServiceDirectories {
    param (
        [string]$WorkingDir
    )
    
    Write-Log "Setting up service directories..."
    
    # Crea le directory necessarie
    $directories = @(
        (Join-Path $WorkingDir "WorkingArea"),
        (Join-Path $WorkingDir "WorkingArea\Config"),
        (Join-Path $WorkingDir "Models"),
        (Join-Path $WorkingDir "config")  # Aggiungiamo directory per client_secret.json
    )
    
    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Log "Created directory: $dir"
        }
    }

    # Copia del file client_secret.json
    $sourceClientSecret = Join-Path $projectPath "client_secret.json"
    $destClientSecret = Join-Path $WorkingDir "config\client_secret.json"
    
    if (Test-Path $sourceClientSecret) {
        Write-Log "Copying client_secret.json..."
        Copy-Item -Path $sourceClientSecret -Destination $destClientSecret -Force
        Write-Log "Copied client_secret.json to service directory"
    } else {
        Write-Log "Warning: client_secret.json not found at: $sourceClientSecret"
    }

    # Gestione del file personalization.json
    $sourcePersonalizationFile = Join-Path $projectPath "personalization.json"
    $destPersonalizationFile = Join-Path $WorkingDir "WorkingArea\personalization.json"
    
    if (Test-Path $sourcePersonalizationFile) {
        Write-Log "Copying existing personalization.json..."
        Copy-Item -Path $sourcePersonalizationFile -Destination $destPersonalizationFile -Force
        Write-Log "Copied personalization.json from project"
    } else {
        Write-Log "Warning: personalization.json not found in project, creating default"
        "{`"settings`": {},`"data`": []}" | Set-Content -Path $destPersonalizationFile -Encoding UTF8
    }
    
    # Crea gli altri file JSON di default
    $jsonFiles = @{
        "WorkingArea\Config\active_memory.json" = "[]"
        "WorkingArea\Config\system_config.json" = "{`"version`": 1,`"settings`": {}}"
    }
    
    foreach ($file in $jsonFiles.Keys) {
        $filePath = Join-Path $WorkingDir $file
        if (-not (Test-Path $filePath)) {
            $jsonFiles[$file] | Set-Content -Path $filePath -Encoding UTF8
            Write-Log "Created file: $filePath"
        }
    }
    
    # Copy required files
    $sourceModelPath = "C:/Models/ggml-base.en.bin"
    $destModelPath = Join-Path $WorkingDir "Models\ggml-base.en.bin"
    
    if (Test-Path $sourceModelPath) {
        Copy-Item -Path $sourceModelPath -Destination $destModelPath -Force
        Write-Log "Copied Whisper model to: $destModelPath"
    } else {
        Write-Log "Warning: Whisper model not found at $sourceModelPath"
    }
}

function Publish-Project {
    Write-Log "Publishing for Windows..."
    
    # Clean up publish directory at the start
    Cleanup-PublishDirectory -Path $publishPath

    try {
        $dotnetVersion = dotnet --version
        Write-Log "Using .NET SDK version: $dotnetVersion"
    } catch {
        throw ".NET SDK is not installed or not in PATH"
    }
    
    # Verify project directory exists
    if (-not (Test-Path $projectPath)) {
        throw "Project directory not found at: $projectPath"
    }
    
    # Look for project file with better error handling
    $projectFile = Get-ChildItem $projectPath -Filter "*.csproj" -ErrorAction Stop | Select-Object -First 1
    if (-not $projectFile) {
        throw "No .csproj file found in $projectPath"
    }
    
    Write-Log "Found project file: $($projectFile.FullName)"
    
    $outputPath = Join-Path $publishPath "win-x64"

    # Ensure publish directory exists
    if (-not (Test-Path $publishPath)) {
        New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
        Write-Log "Created publish directory: $publishPath"
    }
    
    Write-Log "Starting publish process..."
    
    # Execute dotnet publish
    $output = & {
        dotnet publish $projectFile.FullName `
            --configuration $Configuration `
            --runtime win-x64 `
            --self-contained true `
            --output $outputPath `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:IncludeAllContentForSelfExtract=true  # Include tutte le dipendenze
    } *>&1 | Out-String

    if ($LASTEXITCODE -ne 0) {
        Write-Log "Publishing failed with exit code $LASTEXITCODE"
        Write-Log "Error output:"
        $output | Where-Object { $_ -match "error" } | ForEach-Object { Write-Log $_ }
        throw "Publishing failed"
    }
    
    # Verify successful publish
    $exePath = Join-Path $outputPath "iJarvis.exe"
    if (-not (Test-Path $exePath)) {
        throw "Published executable not found at: $exePath"
    }

    # Copy appsettings.json if not included in publish
    $publishedAppSettings = Join-Path $outputPath "appsettings.json"
    if (-not (Test-Path $publishedAppSettings)) {
        Copy-Item -Path $appSettingsSource -Destination $publishedAppSettings -Force
        Write-Log "Copied appsettings.json to publish directory"
    }

    # Copy configuration files from config folder
    $configSourcePath = Join-Path $projectPath "config"
    $configDestPath = Join-Path $outputPath "config"
    
    if (Test-Path $configSourcePath) {
        if (-not (Test-Path $configDestPath)) {
            New-Item -ItemType Directory -Path $configDestPath -Force | Out-Null
        }
        Copy-Item -Path "$configSourcePath\*" -Destination $configDestPath -Recurse -Force
        Write-Log "Copied configuration files from: $configSourcePath"
    } else {
        Write-Log "Warning: Config directory not found at: $configSourcePath"
    }

    # Aggiunta verifica delle dipendenze
    $requiredDlls = @(
        "iJarvis.dll",
        "iJarvis.exe",
        "appsettings.json"
    )
    
    foreach ($dll in $requiredDlls) {
        $dllPath = Join-Path $outputPath $dll
        if (-not (Test-Path $dllPath)) {
            Write-Log "Warning: Required file not found: $dll"
        }
    }
    
    Write-Log "Published successfully to: $outputPath"
    Write-Log "Executable found at: $exePath"

    return $outputPath
}

function Install-WindowsService {
    param ([string]$PublishPath)
    
    Write-Log "Installing Windows service..."
    Ensure-AdminPrivileges
    
    # Stop existing service if running
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq 'Running') {
        Write-Log "Stopping existing service..."
        Stop-Service -Name $serviceName -Force
        Start-Sleep -Seconds 2
    }

    # Kill the process if it is still running
    $process = Get-Process -Name "iJarvis" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Log "Killing existing process..."
        Stop-Process -Name "iJarvis" -Force
        Start-Sleep -Seconds 2
    }

    $workingDir = Join-Path $env:ProgramData $serviceName
    
    # Setup service directories and files
    Setup-ServiceDirectories -WorkingDir $workingDir
    
    # Verify source path exists
    if (-not (Test-Path $PublishPath)) {
        throw "Source path not found: $PublishPath"
    }

    Write-Log "Source path: $PublishPath"
    Write-Log "Destination path: $workingDir"

    # Copy service files preserving structure
    try {
        Write-Log "Copying service files..."
        
        # Prima copiamo i file principali del servizio
        $serviceFiles = @("iJarvis.exe", "appsettings.json", "aspnetcorev2_inprocess.dll", "web.config")
        foreach ($file in $serviceFiles) {
            $source = Join-Path $PublishPath $file
            $dest = Join-Path $workingDir $file
            if (Test-Path $source) {
                Copy-Item -Path $source -Destination $dest -Force
                Write-Log "Copied $file"
            }
        }

        # Poi copiamo la cartella runtimes se esiste
        $runtimesSource = Join-Path $PublishPath "runtimes"
        if (Test-Path $runtimesSource) {
            $runtimesDest = Join-Path $workingDir "runtimes"
            Copy-Item -Path $runtimesSource -Destination $runtimesDest -Recurse -Force
            Write-Log "Copied runtimes folder"
        }

        # Verifichiamo i file critici
        $exePath = Join-Path $workingDir "iJarvis.exe"
        if (-not (Test-Path $exePath)) {
            throw "Service executable not found after copy: $exePath"
        }

        # Set permissions recursively
        Get-ChildItem -Path $workingDir -Recurse | ForEach-Object {
            try {
                $acl = Get-Acl $workingDir
                Set-Acl -Path $_.FullName -AclObject $acl
            } catch {
                Write-Log "Warning: Could not set permissions on: $($_.FullName)"
            }
        }
        
        Write-Log "All service files copied successfully"
    }
    catch {
        Write-Log "Error during file copy: $_"
        throw
    }

    # Remove existing service
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Log "Removing existing service..."
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        sc.exe delete $serviceName
        Start-Sleep -Seconds 2
    }

    # Migliorata la gestione degli errori durante l'installazione
    try {
        Write-Log "Creating new service..."
        $binaryPathName = "`"$exePath`" --service"  # Aggiunto flag --service
        $result = New-Service -Name $serviceName `
            -BinaryPathName $binaryPathName `
            -DisplayName $serviceDisplayName `
            -Description $serviceDescription `
            -StartupType Automatic `
            -ErrorAction Stop

        # Aggiunte dipendenze di rete
        sc.exe config $serviceName depend= Tcpip/Afd
        
        Write-Log "Configuring service..."
        sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
        sc.exe config $serviceName obj= "LocalSystem" password= ""

        # Start service with a timeout
        Write-Log "Starting service..."
        try {
            Start-Service -Name $serviceName
            $timeout = 30
            $timer = [Diagnostics.Stopwatch]::StartNew()
            
            while ($timer.Elapsed.TotalSeconds -lt $timeout) {
                $service = Get-Service -Name $serviceName
                if ($service.Status -eq 'Running') {
                    Write-Log "Service started successfully"
                    break
                }
                if ($service.Status -eq 'Stopped') {
                    # Get the latest error from the event log
                    $event = Get-EventLog -LogName Application -Source "iJarvis Service" -Newest 1 -ErrorAction SilentlyContinue
                    if ($event) {
                        throw "Service failed to start. Error: $($event.Message)"
                    } else {
                        throw "Service failed to start. Check Windows Event Logs for details."
                    }
                }
                Start-Sleep -Seconds 1
            }
            
            if ($timer.Elapsed.TotalSeconds -ge $timeout) {
                throw "Service start timeout after $timeout seconds"
            }
        }
        catch {
            Write-Log "Error starting service: $_"
            # Get the latest error from the event log
            $event = Get-EventLog -LogName Application -Source "iJarvis Service" -Newest 1 -ErrorAction SilentlyContinue
            if ($event) {
                Write-Log "Event Log Error: $($event.Message)"
            }
            throw
        }
        
        # Show final status
        Get-Service -Name $serviceName | Format-List Name, Status, StartType
    }
    catch {
        Write-Log "Failed to create or configure service: $_"
        throw
    }
}

function Uninstall-WindowsService {
    Write-Log "Uninstalling Windows service..."
    Ensure-AdminPrivileges
    
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        # Ferma il servizio con timeout
        if ($service.Status -eq 'Running') {
            Write-Log "Stopping service..."
            Stop-Service -Name $serviceName -Force
            $timeout = 30
            $timer = [Diagnostics.Stopwatch]::StartNew()
            
            while ($timer.Elapsed.TotalSeconds -lt $timeout) {
                $service.Refresh()
                if ($service.Status -eq 'Stopped') {
                    break
                }
                Start-Sleep -Seconds 1
            }
        }
        
        # Assicurati che tutti i processi correlati siano terminati
        Write-Log "Ensuring all related processes are terminated..."
        Get-Process | Where-Object { $_.ProcessName -eq "iJarvis" } | ForEach-Object {
            try {
                $_.Kill()
                $_.WaitForExit(5000)
            } catch {
                Write-Log "Warning: Could not kill process $($_.Id): $_"
            }
        }
        
        Write-Log "Removing service registration..."
        Start-Sleep -Seconds 2
        sc.exe delete $serviceName
        Start-Sleep -Seconds 2
        
        # Rimuovi directory di lavoro con retry
        $workingDir = Join-Path $env:ProgramData $serviceName
        if (Test-Path $workingDir) {
            Write-Log "Removing working directory..."
            $maxAttempts = 3
            $attempt = 1
            
            while ($attempt -le $maxAttempts) {
                try {
                    # Rimuovi attributi di sola lettura da tutti i file
                    Get-ChildItem -Path $workingDir -Recurse -Force | ForEach-Object {
                        if (Test-Path $_.FullName -PathType Leaf) {
                            $attributes = [System.IO.File]::GetAttributes($_.FullName)
                            if ($attributes -band [System.IO.FileAttributes]::ReadOnly) {
                                [System.IO.File]::SetAttributes($_.FullName, $attributes -bxor [System.IO.FileAttributes]::ReadOnly)
                                Write-Log "Removed read-only attribute from: $($_.FullName)"
                            }
                        }
                    }
                    
                    # Prova a rimuovere la directory
                    Remove-Item -Path $workingDir -Recurse -Force -ErrorAction Stop
                    Write-Log "Working directory removed successfully"
                    break
                } catch {
                    if ($attempt -eq $maxAttempts) {
                        Write-Log "Warning: Could not remove working directory after $maxAttempts attempts: $_"
                        Write-Log "Please remove manually: $workingDir"
                    } else {
                        Write-Log "Attempt $attempt failed, retrying in 2 seconds..."
                        Start-Sleep -Seconds 2
                    }
                }
                $attempt++ 
            }
        }
        
        Write-Log "Service uninstalled successfully"
    } else {
        Write-Log "Service not found"
    }
}

function Cleanup-PublishDirectory {
    param ([string]$Path)
    
    Write-Log "Cleaning up publish directory..."
    if (Test-Path $Path) {
        try {
            Remove-Item -Path $Path -Recurse -Force
            Write-Log "Successfully cleaned up publish directory: $Path"
        } catch {
            Write-Log "Warning: Failed to clean up publish directory: $_"
        }
    }
}

function Run-IJarvisDev {
    Write-Log "Running iJarvis in development mode..."
    $projectFile = Get-ChildItem $projectPath -Filter "*.csproj" -ErrorAction Stop | Select-Object -First 1
    if (-not $projectFile) {
        throw "No .csproj file found in $projectPath"
    }

    try {
        $command = "cd '$projectPath'; dotnet run --project '$($projectFile.FullName)'; Write-Host 'Press any key to close...'; `$null = `$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')"
        Start-Process powershell -ArgumentList "-NoExit", "-Command", $command -WindowStyle Normal
        Write-Log "Started iJarvis in new console window"
    }
    catch {
        Write-Log "Error running iJarvis: $_"
        throw
    }
}

function Run-VisualInterfaceDev {
    Write-Log "Running Visual Interface in development mode..."
    $visualInterfacePath = Join-Path $scriptPath "voice-interface-app"
    
    if (-not (Test-Path $visualInterfacePath)) {
        throw "Visual Interface directory not found at: $visualInterfacePath"
    }

    try {
        $command = "cd '$visualInterfacePath'; npm run tauri dev; Write-Host 'Press any key to close...'; `$null = `$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')"
        Start-Process powershell -ArgumentList "-NoExit", "-Command", $command -WindowStyle Normal
        Write-Log "Started Visual Interface in new console window"
    }
    catch {
        Write-Log "Error running Visual Interface: $_"
        throw
    }
}

function Show-Menu {
    Clear-Host
    Write-Host "================ iJarvis Service Manager ================"
    Write-Host ""
    Write-Host "🔧 Development:"
    Write-Host "--------------------------------------------------------"
    Write-Host "1️⃣ 1: Build and Run iJarvis"
    Write-Host "2️⃣ 2: Build and Run Visual Interface"
    Write-Host "3️⃣ 3: Build and Run iJarvis and Visual Interface"  # Add this line
    Write-Host ""
    Write-Host "🔧 Service Management:"
    Write-Host "--------------------------------------------------------"
    Write-Host "4️⃣ 4: Build and Publish Service"
    Write-Host "5️⃣ 5: Install Service"
    Write-Host "6️⃣ 6: Build, Publish, and Install Service"
    Write-Host "7️⃣ 7: Uninstall Service"
    Write-Host ""
    Write-Host "❌ Q: Quit"
    Write-Host ""
    Write-Host "========================================================"
}

function Get-MenuChoice {
    do {
        Show-Menu
        $choice = Read-Host "Please enter your choice"
        
        switch ($choice) {
            '1' { return "dev-ijarvis" }
            '2' { return "dev-visual" }
            '3' { return "dev-both" }  # Add this line
            '4' { return "publish" }
            '5' { return "install" }
            '6' { return "all" }
            '7' { return "uninstall" }
            'Q' { exit }
            'q' { exit }
            default { 
                Write-Host "Invalid choice, please try again."
                Start-Sleep -Seconds 2
            }
        }
    } while ($true)
}

# Modifica la sezione principale
try {
    if (-not $Action -or $Action -eq "all") {
        $Action = Get-MenuChoice
    }

    switch ($Action) {
        "publish" {
            Write-Log "Starting Build and Publish..."
            Publish-Project
        }
        "install" {
            Write-Log "Starting Service Installation..."
            $publishedPath = Join-Path $publishPath "win-x64"
            Install-WindowsService -PublishPath $publishedPath
            Cleanup-PublishDirectory -Path $publishPath
        }
        "uninstall" {
            Write-Log "Starting Service Uninstallation..."
            Uninstall-WindowsService
        }
        "all" {
            Write-Log "Starting Full Deployment..."
            $publishedPath = Publish-Project
            Install-WindowsService -PublishPath $publishedPath
            Cleanup-PublishDirectory -Path $publishPath
        }
        "dev-ijarvis" {
            Write-Log "Starting iJarvis in dev mode..."
            Run-IJarvisDev
        }
        "dev-visual" {
            Write-Log "Starting Visual Interface in dev mode..."
            Run-VisualInterfaceDev
        }
        "dev-both" {  # Add this block
            Write-Log "Starting iJarvis and Visual Interface in dev mode..."
            Run-IJarvisDev
            Run-VisualInterfaceDev
        }
    }
    Write-Log "Operations completed successfully!"
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
} catch {
    Write-Log "Error: $_"
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    exit 1
} 

