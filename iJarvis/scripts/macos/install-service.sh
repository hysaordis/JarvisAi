#!/bin/bash

install_macos_service() {
    local PLIST_TEMPLATE="${SCRIPT_DIR}/macos/alita-service.template.plist"
    local PLIST_PATH="${HOME}/Library/LaunchAgents/com.alita.api.service.plist"
    local APP_PATH="$(cd "${SCRIPT_DIR}/../" && pwd)/AlitaApi"
    local LOG_PATH="${HOME}/Library/Logs/AlitaApi"
    local WORKING_DIR="$(cd "${SCRIPT_DIR}/../" && pwd)"

    # Create logs directory
    mkdir -p "${LOG_PATH}"

    # Generate plist from template
    sed -e "s|{{ALITA_PATH}}|${APP_PATH}|g" \
        -e "s|{{LOG_PATH}}|${LOG_PATH}|g" \
        -e "s|{{WORKING_DIR}}|${WORKING_DIR}|g" \
        "${PLIST_TEMPLATE}" > "${PLIST_PATH}"

    # Set correct permissions
    chmod 644 "${PLIST_PATH}"

    # Unload if exists and load the service
    launchctl unload "${PLIST_PATH}" 2>/dev/null || true
    launchctl load -w "${PLIST_PATH}"

    echo "Alita API service installed successfully on macOS"
}

install_macos_service
