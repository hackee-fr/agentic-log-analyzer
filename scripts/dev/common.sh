#!/usr/bin/env bash
# Shared helpers for the local development scripts (sourced, not executed).

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker/docker-compose.yml"

info() { printf '\033[1;34m▸\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m!\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; exit 1; }

require() {
  command -v "$1" >/dev/null 2>&1 || fail "$1 is required but was not found in PATH."
}

# Frees a TCP port held by a previous Agentic Log Analyzer process; refuses to kill anything else.
free_port() {
  local port="$1" pids pid command
  pids="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || true)"
  [ -z "$pids" ] && return 0
  for pid in $pids; do
    command="$(ps -o command= -p "$pid" 2>/dev/null || true)"
    if [[ "$command" == *AgenticLogAnalyzer* ]]; then
      info "Stopping the previous instance on port $port (PID $pid)."
      kill "$pid" 2>/dev/null || true
      for _ in $(seq 1 20); do kill -0 "$pid" 2>/dev/null || break; sleep 0.25; done
      kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null || true
    else
      fail "Port $port is used by another program (PID $pid: $command). Stop it or choose another port."
    fi
  done
}
