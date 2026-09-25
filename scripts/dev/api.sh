#!/usr/bin/env bash
# Starts the API on http://localhost:5080 with Ollama (Docker) enabled.
#   LLM=off          start without the LLM
#   API_PORT=5080    API port
#   OLLAMA_MODEL=llama3.2
set -euo pipefail
source "$(dirname "$0")/common.sh"

API_PORT="${API_PORT:-5080}"
LLM="${LLM:-ollama}"
OLLAMA_MODEL="${OLLAMA_MODEL:-llama3.2}"
OLLAMA_PORT="${OLLAMA_PORT:-11434}"

require dotnet

if [ "$LLM" = "ollama" ]; then
  if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
    info "Starting Ollama in Docker…"
    OLLAMA_MODEL="$OLLAMA_MODEL" OLLAMA_PORT="$OLLAMA_PORT" docker compose -f "$COMPOSE_FILE" up -d --wait ollama
    if ! docker compose -f "$COMPOSE_FILE" exec -T ollama ollama list | awk 'NR > 1 { print $1 }' | grep -Eq "^${OLLAMA_MODEL}(:latest)?$"; then
      info "Downloading model $OLLAMA_MODEL (first run only, about 2 GB)…"
      OLLAMA_MODEL="$OLLAMA_MODEL" docker compose -f "$COMPOSE_FILE" up ollama-pull
    fi
    export Llm__Provider=ollama
    export Llm__Ollama__BaseUrl="http://localhost:$OLLAMA_PORT"
    export Llm__Ollama__Model="$OLLAMA_MODEL"
    info "LLM enabled: Ollama · $OLLAMA_MODEL"
  else
    warn "Docker is not running: the API starts without the LLM (deterministic answers only)."
  fi
else
  info "LLM disabled (LLM=$LLM)."
fi

free_port "$API_PORT"
info "API → http://localhost:$API_PORT (Ctrl+C to stop)"
cd "$ROOT"
exec dotnet run --project apps/api/AgenticLogAnalyzer.Api -- --urls "http://localhost:$API_PORT"
