#!/bin/bash

# Default values
ACTION="all"
PLATFORM="both"
CONFIGURATION="Release"

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
PUBLISH_DIR="$PROJECT_DIR/publish"

# Functions
log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S'): $1"
}

publish_project() {
    local runtime=$1
    log "Publishing for $runtime..."
    local output_path="$PUBLISH_DIR/$runtime"
    
    dotnet publish "$PROJECT_DIR" \
        --configuration "$CONFIGURATION" \
        --runtime "$runtime" \
        --self-contained true \
        --output "$output_path" \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true
    
    if [ $? -ne 0 ]; then
        log "Error: Publishing failed for $runtime"
        exit 1
    fi
    
    log "Published successfully to: $output_path"
    echo "$output_path"
}

install_service() {
    local runtime=$1
    local publish_path=$2
    
    log "Installing service for $runtime..."
    
    if [ "$runtime" = "win-x64" ]; then
        if [[ "$OSTYPE" == "msys"* ]] || [[ "$OSTYPE" == "cygwin"* ]]; then
            powershell.exe -ExecutionPolicy Bypass -File "$PROJECT_DIR/scripts/windows/install-service.ps1" -Install -Configuration "$CONFIGURATION"
        else
            log "Cannot install Windows service from non-Windows system"
        fi
    elif [ "$runtime" = "osx-x64" ]; then
        if [[ "$OSTYPE" == "darwin"* ]]; then
            chmod +x "$PROJECT_DIR/scripts/macos/install-service.sh"
            "$PROJECT_DIR/scripts/macos/install-service.sh"
        else
            log "Cannot install macOS service from non-macOS system"
        fi
    fi
}

process_platform() {
    local runtime=$1
    local output_path=""
    
    if [ "$ACTION" = "publish" ] || [ "$ACTION" = "all" ]; then
        output_path=$(publish_project "$runtime")
    fi
    
    if [ "$ACTION" = "install" ] || [ "$ACTION" = "all" ]; then
        install_service "$runtime" "$output_path"
    fi
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --action)
        ACTION="$2"
        shift 2
        ;;
        --platform)
        PLATFORM="$2"
        shift 2
        ;;
        --configuration)
        CONFIGURATION="$2"
        shift 2
        ;;
        *)
        log "Unknown option: $1"
        exit 1
        ;;
    esac
done

# Create publish directory
mkdir -p "$PUBLISH_DIR"

# Process requested platforms
if [ "$PLATFORM" = "win" ] || [ "$PLATFORM" = "both" ]; then
    process_platform "win-x64"
fi

if [ "$PLATFORM" = "osx" ] || [ "$PLATFORM" = "both" ]; then
    process_platform "osx-x64"
fi

log "Operations completed successfully!"
