# ALITA AI SERVICE - INSTALLATION GUIDE

## 1. PREREQUISITES

- .NET 7.0 SDK or later
- PowerShell 7.0+ (Windows)
- Bash shell (macOS)
- Administrator/sudo privileges

## 2. QUICK INSTALLATION

### WINDOWS:

Open PowerShell as Administrator and run:

```powershell
.\scripts\build-and-install.ps1 -Action all -Platform win -Configuration Release
```

### MACOS:

Open Terminal and run:

```bash
./scripts/build-and-install.sh --action all --platform osx --configuration Release
```

## 3. MANUAL INSTALLATION

### WINDOWS:

1. Build:
   ```
   dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ./publish/win-x64
   ```
2. Install Service (As Administrator):
   ```powershell
   .\scripts\windows\install-service.ps1 -Install
   ```
3. Service Commands:
   - Check status: `Get-Service AlitaApiService`
   - Start: `Start-Service AlitaApiService`
   - Stop: `Stop-Service AlitaApiService`
   - Uninstall: `.\scripts\windows\install-service.ps1 -Uninstall`

### MACOS:

1. Build:
   ```
   dotnet publish --configuration Release --runtime osx-x64 --self-contained true --output ./publish/osx-x64
   ```
2. Install:
   ```
   ./scripts/macos/install-service.sh
   ```
3. Service Commands:
   - Check status: `launchctl list | grep com.alita.api.service`
   - Start: `launchctl load ~/Library/LaunchAgents/com.alita.api.service.plist`
   - Stop: `launchctl unload ~/Library/LaunchAgents/com.alita.api.service.plist`
   - Remove: `rm ~/Library/LaunchAgents/com.alita.api.service.plist`

## 4. CONFIGURATION

- Default Port: 5000
- Log Locations:
  - Windows: `%ProgramData%\AlitaApi\logs`
  - macOS: `~/Library/Logs/AlitaApi`

## 5. TROUBLESHOOTING

### WINDOWS:

1. Check Windows Event Viewer
2. Review logs in `%ProgramData%\AlitaApi\logs`
3. Verify PowerShell is running as Administrator
4. Ensure .NET 7.0 SDK is installed
5. Check service status in Services.msc

### MACOS:

1. Check Console.app for logs
2. Review logs in `~/Library/Logs/AlitaApi`
3. Verify launch agent permissions
4. Ensure Bash shell has execute permissions
5. Check .NET SDK installation

## 6. BUILD SCRIPT OPTIONS

### Windows (build-and-install.ps1):

Parameters:

- `-Action`: `publish|install|all`
- `-Platform`: `win|osx|both`
- `-Configuration`: `Debug|Release`

Example:

```powershell
.\scripts\build-and-install.ps1 -Action publish -Platform win -Configuration Release
```

### macOS (build-and-install.sh):

Parameters:

- `--action`: `publish|install|all`
- `--platform`: `win|osx|both`
- `--configuration`: `Debug|Release`

Example:

```bash
./scripts/build-and-install.sh --action publish --platform osx --configuration Release
```

## 7. ADDITIONAL NOTES

- Always backup your data before installation
- Service runs under SYSTEM account on Windows
- Service runs under user context on macOS
- Default timeout for service operations: 30 seconds
- Logs are rotated automatically
- API endpoint available at `http://localhost:5000`
