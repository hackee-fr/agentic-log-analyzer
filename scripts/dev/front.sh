#!/usr/bin/env bash
# Builds the React dashboard and serves it on http://localhost:5081.
#   FRONT_PORT=5081
set -euo pipefail
source "$(dirname "$0")/common.sh"

FRONT_PORT="${FRONT_PORT:-5081}"
WEB="$ROOT/apps/dashboard/AgenticLogAnalyzer.Dashboard/web"

require dotnet
require pnpm

cd "$WEB"
if [ ! -d node_modules ]; then
  info "Installing dashboard dependencies…"
  pnpm install --frozen-lockfile
fi
info "Building the dashboard…"
pnpm build

free_port "$FRONT_PORT"
info "Dashboard → http://localhost:$FRONT_PORT (Ctrl+C to stop)"
cd "$ROOT"
exec dotnet run --project apps/dashboard/AgenticLogAnalyzer.Dashboard -- --urls "http://localhost:$FRONT_PORT"
