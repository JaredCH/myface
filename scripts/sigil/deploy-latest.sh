#!/bin/bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-/home/nessy/myface/myface}"
BRANCH="${BRANCH:-main}"
FORCE_UPDATE="${FORCE_UPDATE:-false}"

cd "$REPO_DIR"

log() {
    printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

if [[ "$FORCE_UPDATE" != "true" ]] && [ -n "$(git status --porcelain)" ]; then
    echo "[fail] Repository has local changes. Commit or set FORCE_UPDATE=true to overwrite." >&2
    exit 1
fi

log "Fetching origin/$BRANCH ..."
git fetch origin "$BRANCH" --prune

log "Resetting working tree ..."
git reset --hard "origin/$BRANCH"
git submodule update --init --recursive

log "Running redeploy pipeline ..."
"$REPO_DIR/redeploy.sh"
