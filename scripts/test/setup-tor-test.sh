#!/bin/bash
set -euo pipefail

# Setup test Tor configuration

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd ../.. && pwd)"
TORRC_TEST="$ROOT/tor/torrc.test"
HS_TEST_DIR="$ROOT/tor/hidden_service_test"
DATA_TEST_DIR="$ROOT/tor/data_test"

log() {
    printf '[TEST] [%s] %s\n' "$(date -u +%H:%M:%S)" "$*"
}

mkdir -p "$HS_TEST_DIR"
mkdir -p "$DATA_TEST_DIR"
chmod 700 "$HS_TEST_DIR"
chmod 700 "$DATA_TEST_DIR"

log "Creating test Tor configuration at $TORRC_TEST ..."

cat > "$TORRC_TEST" <<EOF
DataDirectory $DATA_TEST_DIR
HiddenServiceDir $HS_TEST_DIR/
HiddenServicePort 80 127.0.0.1:5001
SocksPort 9053
ControlPort 9054
HiddenServiceEnableIntroDoSDefense 1
HiddenServicePoWDefensesEnabled 1
HiddenServicePoWQueueRate 250
HiddenServicePoWQueueBurst 2500
EOF

log "Test Tor config created"
log "Hidden service directory: $HS_TEST_DIR"
log "SOCKS port: 9053 (different from production 9052)"
log "Web port: 5001 (different from production 5000)"
