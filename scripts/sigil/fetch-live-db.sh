#!/bin/bash
set -euo pipefail

REMOTE_SSH="${REMOTE_SSH:-nessy@173.212.225.125}"
REMOTE_BACKUP_DIR="${REMOTE_BACKUP_DIR:-/home/nessy/backups/sigil}"
REMOTE_PATTERN="${REMOTE_PATTERN:-postgres-myface.sql.gz}"
LOCAL_WORK_DIR="${LOCAL_WORK_DIR:-$HOME/sigil-db-sync}"
LOCAL_DB_NAME="${LOCAL_DB_NAME:-myface_test}"
LOCAL_DB_USER="${LOCAL_DB_USER:-postgres}"
LOCAL_DB_HOST="${LOCAL_DB_HOST:-localhost}"
IMPORT_AFTER="${IMPORT_AFTER:-false}"

if [[ "${1:-}" == "--import" ]]; then
    IMPORT_AFTER=true
    shift
fi

mkdir -p "$LOCAL_WORK_DIR"

log() {
    printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

require() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "[fail] required command '$1' not found" >&2
        exit 1
    fi
}

require ssh
require rsync
require gzip
if [[ "$IMPORT_AFTER" == "true" ]]; then
    require psql
fi

log "Locating newest backup on $REMOTE_SSH ..."
LATEST_DIR=$(ssh "$REMOTE_SSH" "ls -1dt ${REMOTE_BACKUP_DIR}/20* 2>/dev/null | head -n 1")
if [ -z "$LATEST_DIR" ]; then
    echo "[fail] no backups found under $REMOTE_BACKUP_DIR" >&2
    exit 1
fi

REMOTE_FILE=$(ssh "$REMOTE_SSH" "ls -1 ${LATEST_DIR}/${REMOTE_PATTERN} 2>/dev/null | head -n 1")
if [ -z "$REMOTE_FILE" ]; then
    echo "[fail] pattern $REMOTE_PATTERN not found in $LATEST_DIR" >&2
    exit 1
fi

log "Downloading $(basename "$REMOTE_FILE") ..."
if ! rsync -av "$REMOTE_SSH:$REMOTE_FILE" "$LOCAL_WORK_DIR/"; then
    echo "[fail] rsync failed" >&2
    exit 1
fi
LOCAL_FILE="$LOCAL_WORK_DIR/$(basename "$REMOTE_FILE")"
log "Saved to $LOCAL_FILE"

if [[ "$IMPORT_AFTER" == "true" ]]; then
    log "Importing into local database $LOCAL_DB_NAME ..."
    gunzip -c "$LOCAL_FILE" | psql --host="$LOCAL_DB_HOST" --username="$LOCAL_DB_USER" "$LOCAL_DB_NAME"
    log "Import complete"
else
    log "Skipping import (set IMPORT_AFTER=true or pass --import to load automatically)"
fi
