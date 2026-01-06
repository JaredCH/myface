#!/bin/bash
set -euo pipefail

# Stop test environment (Tor + MyFace web server)

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd ../.. && pwd)"
PID_FILE="$ROOT/test-web.pid"
TORRC_TEST="$ROOT/tor/torrc.test"

log() {
    printf '[TEST] [%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

log "Stopping test environment..."

# Stop web server
if [ -f "$PID_FILE" ]; then
    PID=$(cat "$PID_FILE")
    if kill -0 "$PID" 2>/dev/null; then
        log "Stopping test web server (PID $PID)"
        kill "$PID" || true
    fi
    rm -f "$PID_FILE"
fi

# Kill any remaining test web processes on port 5001
if lsof -ti:5001 >/dev/null 2>&1; then
    log "Killing processes on port 5001"
    lsof -ti:5001 | xargs kill -9 2>/dev/null || true
fi

# Stop test Tor
if [ -f "$TORRC_TEST" ]; then
    if pgrep -a -f -- "-f $TORRC_TEST" >/dev/null 2>&1; then
        log "Stopping test Tor instance"
        pkill -f "-f $TORRC_TEST" || true
    fi
fi

log "Test environment stopped"
