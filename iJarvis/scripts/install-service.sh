#!/bin/bash

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SERVICE_NAME="AlitaAI"

if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    source "${SCRIPT_DIR}/macos/install-service.sh"
elif [[ "$OSTYPE" == "msys"* ]] || [[ "$OSTYPE" == "cygwin"* ]]; then
    # Windows
    powershell.exe -ExecutionPolicy Bypass -File "${SCRIPT_DIR}/windows/install-service.ps1" -Install
else
    echo "Unsupported operating system"
    exit 1
fi
