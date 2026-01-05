#!/bin/bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-/home/nessy/myface/myface}"
PID_FILE="$PROJECT_ROOT/web.pid"
TORRC="$PROJECT_ROOT/tor/torrc"

log() {
    printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

stop_web() {
    if [ -f "$PID_FILE" ]; then
        PID=$(cat "$PID_FILE")
        if kill -0 "$PID" >/dev/null 2>&1; then
            log "Stopping MyFace.Web pid $PID"
            kill "$PID" || true
        fi
        rm -f "$PID_FILE"
    fi

    if pgrep -f "MyFace.Web.dll" >/dev/null 2>&1; then
        log "Killing remaining MyFace.Web processes"
        pkill -f "MyFace.Web.dll" || true
    fi
}

stop_tor() {
    if pgrep -a -f -- "-f $TORRC" >/dev/null 2>&1; then
        log "Stopping project Tor instance"
        pkill -f "-f $TORRC" || true
    else
        log "No project Tor process detected"
    fi
}

log "Standing down MyFace tor deployment..."
stop_web
stop_tor

log "Done"
