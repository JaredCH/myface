#!/bin/bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-/home/nessy/myface/myface}"
BACKUP_ROOT="${BACKUP_ROOT:-/home/nessy/backups/sigil}"
NEUTRAL_TARGET="${NEUTRAL_TARGET:-}"
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-myface}"
PGUSER="${PGUSER:-postgres}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
RUN_DIR="$BACKUP_ROOT/$TIMESTAMP"
mkdir -p "$RUN_DIR"

log() {
    printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

require() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "[fail] required command '$1' not found" >&2
        exit 1
    fi
}

require pg_dump
require tar
if [ -n "$NEUTRAL_TARGET" ]; then
    require rsync
fi

log "Dumping PostgreSQL database '$PGDATABASE'..."
PGDUMP_FILE="$RUN_DIR/postgres-${PGDATABASE}.sql.gz"
if ! pg_dump --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" "$PGDATABASE" | gzip > "$PGDUMP_FILE"; then
    echo "[fail] pg_dump failed" >&2
    exit 1
fi

log "Archiving Tor hidden service keys..."
TOR_SRC="$PROJECT_ROOT/tor"
tar -czf "$RUN_DIR/tor-hidden-service.tar.gz" -C "$TOR_SRC" hidden_service torrc

if [ -d "$PROJECT_ROOT/wwwroot/uploads" ]; then
    log "Archiving uploaded media..."
    tar -czf "$RUN_DIR/wwwroot-uploads.tar.gz" -C "$PROJECT_ROOT/wwwroot" uploads
else
    log "No uploaded media directory detected"
fi

log "Copying configuration snapshots..."
cp "$PROJECT_ROOT/MyFace.Web/appsettings.json" "$RUN_DIR/appsettings.json"
cp "$PROJECT_ROOT/MyFace.Web/appsettings.Development.json" "$RUN_DIR/appsettings.Development.json"
cp "$PROJECT_ROOT/redeploy.sh" "$RUN_DIR/redeploy.sh"
cp "$PROJECT_ROOT/start-tor.sh" "$RUN_DIR/start-tor.sh"

log "Writing manifest..."
cat > "$RUN_DIR/manifest.txt" <<MANIFEST
Timestamp: $TIMESTAMP
ProjectRoot: $PROJECT_ROOT
Database: $PGDATABASE@$PGHOST:$PGPORT
Artifacts:
  - $(basename "$PGDUMP_FILE")
  - tor-hidden-service.tar.gz
  - wwwroot-uploads.tar.gz (if present)
  - appsettings.json
  - appsettings.Development.json
  - redeploy.sh
  - start-tor.sh
MANIFEST

if [ -n "$NEUTRAL_TARGET" ]; then
    DEST_PATH="$NEUTRAL_TARGET/$TIMESTAMP"
    log "Syncing bundle to $DEST_PATH ..."
    rsync -a "$RUN_DIR/" "$DEST_PATH/"
fi

if [[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]]; then
    log "Pruning backups older than $RETENTION_DAYS days..."
    find "$BACKUP_ROOT" -maxdepth 1 -mindepth 1 -type d -name '20*' -mtime +"$RETENTION_DAYS" -print -exec rm -rf {} +
fi

log "Backup complete: $RUN_DIR"
