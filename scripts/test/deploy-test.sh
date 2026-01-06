#!/bin/bash
set -euo pipefail

# Test environment deployment script
# Builds and deploys MyFace to the test .onion address

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd ../.. && pwd)"
PROJECT_DIR="$ROOT/MyFace.Web"
PUBLISH_DIR="$ROOT/publish-dev"
TEST_ENV="${ASPNETCORE_ENVIRONMENT:-Test}"

log() {
    printf '[TEST] [%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

cd "$ROOT"

log "Building MyFace for test environment..."
dotnet build --configuration Release

log "Publishing to $PUBLISH_DIR ..."
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT_DIR" \
    --configuration Release \
    --output "$PUBLISH_DIR" \
    --no-build

log "Test build complete!"
log "Run './scripts/test/start-test.sh' to start the test server"
