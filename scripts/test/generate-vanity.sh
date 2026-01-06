#!/bin/bash
set -euo pipefail

# Generate a vanity .onion address starting with specified prefix
# Usage: ./generate-vanity.sh <prefix> [threads]

PREFIX="${1:-sigte}"
THREADS="${2:-4}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd ../.. && pwd)"
MKPY224_BIN="$ROOT/tor/vanity/mkp224o/mkp224o"
OUTPUT_DIR="$ROOT/tor/vanity/output"
TEST_HS_DIR="$ROOT/tor/hidden_service_test"

if [ ! -x "$MKPY224_BIN" ]; then
    echo "[fail] mkp224o not found or not executable at $MKPY224_BIN" >&2
    echo "       Build it first: cd $ROOT/tor/vanity/mkp224o && ./autogen.sh && ./configure && make" >&2
    exit 1
fi

log() {
    printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

log "Generating vanity address starting with '$PREFIX' using $THREADS threads..."
log "This may take several minutes depending on prefix length..."

mkdir -p "$OUTPUT_DIR"
cd "$OUTPUT_DIR"

# Run mkp224o - it will create a directory with the full .onion hostname
"$MKPY224_BIN" -d "$OUTPUT_DIR" -n 1 -t "$THREADS" "$PREFIX"

# Find the generated directory (newest one matching prefix)
GENERATED=$(find "$OUTPUT_DIR" -maxdepth 1 -type d -name "${PREFIX}*" -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

if [ -z "$GENERATED" ] || [ ! -d "$GENERATED" ]; then
    echo "[fail] vanity generation failed - no output directory found" >&2
    exit 1
fi

ONION_HOSTNAME=$(basename "$GENERATED")
log "SUCCESS! Generated: $ONION_HOSTNAME"

# Copy to test hidden service directory
log "Installing keys to $TEST_HS_DIR ..."
mkdir -p "$TEST_HS_DIR"
cp -v "$GENERATED"/* "$TEST_HS_DIR/"
chmod 700 "$TEST_HS_DIR"
chmod 600 "$TEST_HS_DIR"/*

log "Test hidden service ready at: $ONION_HOSTNAME"
log "Keys installed to: $TEST_HS_DIR"
echo "$ONION_HOSTNAME"
