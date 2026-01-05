#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE_HOST="${1:-}"
REMOTE_DIR="${2:-/home/nessy/myface/myface}"
DB_NAME="${DB_NAME:-myface}"
LOCAL_DB_USER="${LOCAL_DB_USER:-postgres}"
REMOTE_DB_USER="${REMOTE_DB_USER:-postgres}"
REMOTE_DB_NAME="${REMOTE_DB_NAME:-$DB_NAME}"
SSH_BIN="${SSH_BIN:-ssh}"
RSYNC_BIN="${RSYNC_BIN:-rsync}"
SCP_BIN="${SCP_BIN:-scp}"

if [[ -z "$REMOTE_HOST" ]]; then
    echo "Usage: $0 <user@remote-host> [remote-dir]" >&2
    exit 1
fi

command -v "$RSYNC_BIN" >/dev/null 2>&1 || { echo "rsync is required" >&2; exit 1; }
command -v "$SSH_BIN" >/dev/null 2>&1 || { echo "ssh is required" >&2; exit 1; }
command -v "$SCP_BIN" >/dev/null 2>&1 || { echo "scp is required" >&2; exit 1; }
command -v pg_dump >/dev/null 2>&1 || { echo "pg_dump is required" >&2; exit 1; }

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
DB_DUMP="$TMP_DIR/${DB_NAME}-$(date +%Y%m%d%H%M%S).dump"

if command -v sudo >/dev/null 2>&1; then
    echo "[local] dumping database $DB_NAME as $LOCAL_DB_USER"
    sudo -u "$LOCAL_DB_USER" pg_dump -Fc "$DB_NAME" > "$DB_DUMP"
else
    echo "[local] dumping database $DB_NAME via pg_dump"
    pg_dump -Fc -U "$LOCAL_DB_USER" "$DB_NAME" > "$DB_DUMP"
fi

echo "[remote] ensuring target directory exists"
$SSH_BIN "$REMOTE_HOST" "mkdir -p '$REMOTE_DIR'"

echo "[remote] synchronizing project files"
$RSYNC_BIN -az --delete -e "$SSH_BIN" "$ROOT/" "$REMOTE_HOST:$REMOTE_DIR"

REMOTE_DUMP="$REMOTE_DIR/$(basename "$DB_DUMP")"
echo "[remote] uploading database dump"
$SCP_BIN "$DB_DUMP" "$REMOTE_HOST:$REMOTE_DUMP"

read -r -d '' REMOTE_DB_SCRIPT <<'EOF'
set -euo pipefail
cd "$REMOTE_DIR"
if ! command -v sudo >/dev/null 2>&1; then
    echo "sudo is required on the remote host" >&2
    exit 1
fi
run_db() {
    sudo -u "$REMOTE_DB_USER" "$@"
}
if run_db psql -d postgres -tc "SELECT 1" >/dev/null 2>&1; then
    echo "[remote] restoring database $REMOTE_DB_NAME"
    run_db dropdb --if-exists "$REMOTE_DB_NAME"
    run_db createdb "$REMOTE_DB_NAME"
    run_db pg_restore --clean --if-exists -d "$REMOTE_DB_NAME" "$REMOTE_DUMP"
else
    echo "[remote] unable to connect as $REMOTE_DB_USER; skipping database restore" >&2
fi
EOF

$SSH_BIN "$REMOTE_HOST" \
    "REMOTE_DIR='$REMOTE_DIR' REMOTE_DB_USER='$REMOTE_DB_USER' REMOTE_DB_NAME='$REMOTE_DB_NAME' REMOTE_DUMP='$REMOTE_DUMP' bash -s" \
    <<<"$REMOTE_DB_SCRIPT"

echo "[remote] clone complete"
