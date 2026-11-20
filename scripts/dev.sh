#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_BIN="${EVENTHUB_DOTNET:-$(command -v dotnet || true)}"
if [ -z "$DOTNET_BIN" ] && [ -x /workspace/.dotnet/dotnet ]; then DOTNET_BIN=/workspace/.dotnet/dotnet; fi
if [ -z "$DOTNET_BIN" ]; then echo 'Install the .NET 10 SDK before starting EventHub.' >&2; exit 1; fi
WEB_PORT="${PORT:-3000}"
WEB_HOST=0.0.0.0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --port|-p) WEB_PORT="$2"; shift 2 ;;
    --port=*) WEB_PORT="${1#*=}"; shift ;;
    --host|--hostname) WEB_HOST="$2"; shift 2 ;;
    *) shift ;;
  esac
done
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5050
"$DOTNET_BIN" run --project backend/EventHub.Api --no-launch-profile &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT INT TERM
npm --prefix web run dev -- --hostname "$WEB_HOST" --port "$WEB_PORT"
