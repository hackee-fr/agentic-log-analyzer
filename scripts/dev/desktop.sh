#!/usr/bin/env bash
# Builds the dashboard and runs the desktop app (native window, API in-process).
set -euo pipefail
source "$(dirname "$0")/common.sh"

require dotnet
require pnpm

WEB="$ROOT/apps/dashboard/AgenticLogAnalyzer.Dashboard/web"
cd "$WEB"
[ -d node_modules ] || { info "Installing dashboard dependencies…"; pnpm install --frozen-lockfile; }
info "Building the dashboard…"
pnpm build

info "Starting the desktop app (close the window to quit)…"
cd "$ROOT"
exec dotnet run --project apps/desktop/AgenticLogAnalyzer.Desktop
