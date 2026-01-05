#!/bin/bash
set -euo pipefail

# Lightweight redeploy helper for MyFace + Tor stack.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$ROOT/publish/MyFace.Web"
WEB_DLL="$PUBLISH_DIR/MyFace.Web.dll"
LOG_FILE="$ROOT/web.out"
PID_FILE="$ROOT/web.pid"
BASE_URL="${BASE_URL:-http://localhost:5000}"
ONION_HOST_FILE="$ROOT/tor/hidden_service/hostname"
ONION_HOSTNAME=""
if [ -f "$ONION_HOST_FILE" ]; then
    ONION_HOSTNAME="$(tr -d '\r\n' < "$ONION_HOST_FILE")"
fi
TOR_SOCKS_PORT="${TOR_SOCKS_PORT:-9052}"

install_service() {
    local name="$1"
    case "$name" in
        privoxy)
            if ! command -v apt-get >/dev/null 2>&1; then
                echo "[warn] apt-get not available; cannot install $name" >&2
                return 1
            fi
            echo "[action] installing $name via apt-get"
            sudo apt-get update
            sudo apt-get install -y privoxy
            sudo systemctl enable --now privoxy.service
            ;;
        *)
            echo "[warn] auto-install not configured for $name" >&2
            return 1
            ;;
    esac
}

check_service() {
    local name="$1"
    if command -v systemctl >/dev/null 2>&1; then
        local unit="$name"
        [[ "$unit" != *.service ]] && unit+=".service"

        if ! systemctl list-unit-files "$unit" >/dev/null 2>&1; then
            if install_service "$name"; then
                echo "[ok] installed $name service"
            else
                echo "[fail] $name service missing and auto-install failed" >&2
                return 1
            fi
        fi

        if systemctl is-active --quiet "$unit"; then
            echo "[ok] $name is active"
        else
            echo "[action] restarting $name"
            sudo systemctl restart "$unit"
            if ! systemctl is-active --quiet "$unit"; then
                echo "[fail] $name is not active after restart" >&2
                exit 1
            fi
        fi
    else
        if pgrep -x "$name" >/dev/null 2>&1 || pgrep -f "$name" >/dev/null 2>&1; then
            echo "[ok] $name process found"
        else
            echo "[warn] systemctl not available and no $name process found"
        fi
    fi
}

check_url() {
    local url="$1"
    local label="$2"
    shift 2
    local code
    if ! code=$(curl -s -o /dev/null -w "%{http_code}" "$@" "$url" 2>/dev/null); then
        echo "[warn] $label request failed (curl error)"
        return 0
    fi

    echo "[check] $label -> $code"
    if [[ "$code" == "200" || "$code" == "302" ]]; then
        echo "[ok] $label responding (200/302)"
    else
        echo "[warn] $label unexpected status (see $LOG_FILE)"
    fi
}

echo "--- checking supporting services (nginx, tor, privoxy) ---"
for svc in nginx tor privoxy; do
    check_service "$svc" || exit 1
done

echo "--- ensuring bundled tor hidden service ---"
if ! "$ROOT/start-tor.sh"; then
    echo "[fail] unable to start project tor instance" >&2
    exit 1
fi

echo "--- publishing MyFace.Web (Release) ---"
if [ -f "$PUBLISH_DIR" ]; then
    echo "[cleanup] removing stale file at $PUBLISH_DIR"
    rm -f "$PUBLISH_DIR"
fi

echo "--- running database migrations ---"
dotnet ef database update \
    --project "$ROOT/MyFace.Data/MyFace.Data.csproj" \
    --context ApplicationDbContext

dotnet publish "$ROOT/MyFace.Web/MyFace.Web.csproj" -c Release -o "$PUBLISH_DIR"

if [ ! -f "$WEB_DLL" ]; then
    echo "[fail] publish output missing: $WEB_DLL" >&2
    exit 1
fi

echo "--- stopping existing MyFace.Web instances ---"
pkill -f "MyFace.Web.dll" >/dev/null 2>&1 || true
pkill -f "MyFace.Web --no-launch-profile" >/dev/null 2>&1 || true

if [ -f "$PID_FILE" ]; then
    if kill -0 "$(cat "$PID_FILE")" >/dev/null 2>&1; then
        kill "$(cat "$PID_FILE")" || true
    fi
    rm -f "$PID_FILE"
fi


echo "--- starting MyFace.Web (Production, port 5000) ---"
nohup env \
    ASPNETCORE_URLS="$BASE_URL" \
    ASPNETCORE_ENVIRONMENT="Production" \
    sh -c "cd '$PUBLISH_DIR' && dotnet '$WEB_DLL'" \
    > "$LOG_FILE" 2>&1 &
NEW_PID=$!
echo "$NEW_PID" > "$PID_FILE"
echo "[ok] started MyFace.Web pid=$NEW_PID"

sleep 3
echo "--- status checks ---"
check_url "$BASE_URL/" "local root"
check_url "$BASE_URL/Mail" "local /Mail"

if [ -n "$ONION_HOSTNAME" ]; then
    check_url "http://$ONION_HOSTNAME/" "onion root" --socks5-hostname 127.0.0.1:$TOR_SOCKS_PORT
fi

echo "[info] Local URL: $BASE_URL/"
if [ -n "$ONION_HOSTNAME" ]; then
    echo "[info] Onion URL: http://$ONION_HOSTNAME/"
fi

echo "--- done ---"
