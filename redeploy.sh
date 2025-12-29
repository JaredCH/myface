#!/bin/bash
set -euo pipefail

# Lightweight redeploy helper for MyFace + Tor stack.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$ROOT/publish/MyFace.Web"
WEB_DLL="$PUBLISH_DIR/MyFace.Web.dll"
LOG_FILE="$ROOT/web.out"
PID_FILE="$ROOT/web.pid"

check_service() {
    local name="$1"
    if command -v systemctl >/dev/null 2>&1; then
        if systemctl is-active --quiet "$name"; then
            echo "[ok] $name is active"
        else
            echo "[action] restarting $name"
            sudo systemctl restart "$name"
            if ! systemctl is-active --quiet "$name"; then
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
    ASPNETCORE_URLS="http://localhost:5000" \
    ASPNETCORE_ENVIRONMENT="Production" \
    sh -c "cd '$PUBLISH_DIR' && dotnet '$WEB_DLL'" \
    > "$LOG_FILE" 2>&1 &
NEW_PID=$!
echo "$NEW_PID" > "$PID_FILE"
echo "[ok] started MyFace.Web pid=$NEW_PID"

sleep 2
echo "--- health check /Mail ---"
CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/Mail || echo "curl_failed")
echo "[check] GET /Mail -> $CODE"

if [[ "$CODE" == "200" || "$CODE" == "302" ]]; then
    echo "[ok] service responding (200/302 expected if auth redirect)"
else
    echo "[warn] unexpected status from /Mail (see $LOG_FILE)"
fi

echo "--- done ---"
