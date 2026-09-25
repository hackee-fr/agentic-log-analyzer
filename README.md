# Agentic Log Analyzer

Extensible log ingestion, normalization, search, detection, correlation, and agent-assisted investigation platform built with C#/.NET.

## Architecture

`Sources → Connectors → Parsing → Normalization → Storage → Detection/Search → Correlation → Tools → Skills → Agents → LLM → Investigation → Report`

The deterministic engine works without an LLM. The initial target is PostgreSQL for storage and Ollama for local LLM inference.

## Monorepo

- `apps/api` — ASP.NET Core API
- `apps/worker` — background ingestion/processing worker
- `apps/dashboard` — web dashboard
- `packages/domain` — domain model
- `packages/application` — use cases and abstractions
- `packages/infrastructure` — persistence and external infrastructure
- `packages/connectors` — log source connectors
- `packages/detection` — deterministic detections
- `packages/correlation` — event correlation
- `packages/agentic` — tools, skills, and agent orchestration
- `packages/llm` — LLM provider implementations
- `tests` — unit, integration, and end-to-end tests
- `knowledge` — source and protocol knowledge
- `infrastructure` — Docker, PostgreSQL, and Hyper-V lab assets

## Getting started

Install the .NET 10 SDK, then run:

```powershell
dotnet restore
dotnet build
dotnet test
```

The project is intentionally being built incrementally. The first milestone is a reliable log engine before agentic features are introduced.

## Security

Never commit real professional logs, credentials, API keys, tokens, or sensitive identifiers. Use anonymized fixtures for tests.
