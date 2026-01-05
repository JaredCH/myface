#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOR_DIR="$ROOT/tor"
TORRC="$TOR_DIR/torrc"
TORRC_REL="${TORRC#$ROOT/}"
HS_DIR="$TOR_DIR/hidden_service"
LOG_FILE="$TOR_DIR/tor.log"

if [ ! -f "$TORRC" ]; then
	echo "[fail] Tor config not found at $TORRC" >&2
	exit 1
fi

mkdir -p "$TOR_DIR/data"
chmod 700 "$HS_DIR"

if pgrep -a -f -- "-f $TORRC" >/dev/null 2>&1 || { [ -n "$TORRC_REL" ] && pgrep -a -f -- "-f $TORRC_REL" >/dev/null 2>&1; }; then
	echo "[ok] Tor hidden service already running"
	exit 0
fi

echo "Starting Tor with config $TORRC ..."
tor --RunAsDaemon 1 -f "$TORRC" --Log "notice file $LOG_FILE"
echo "[ok] Tor started (log: $LOG_FILE)"
