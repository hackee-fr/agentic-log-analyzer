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

## Run locally

Install the .NET 10 SDK, Node.js 20+, and pnpm. Build the React dashboard, then start the API and dashboard in separate terminals from the repository root.

```sh
cd apps/dashboard/AgenticLogAnalyzer.Dashboard/web
pnpm install --frozen-lockfile
pnpm build
```

In terminal 1, start the API:

```sh
dotnet run --project apps/api/AgenticLogAnalyzer.Api -- --urls http://localhost:5080
```

In terminal 2, start the dashboard:

```sh
dotnet run --project apps/dashboard/AgenticLogAnalyzer.Dashboard -- --urls http://localhost:5081
```

Open `http://localhost:5081`. The dashboard imports `.log` and `.txt` files or pasted lines, searches events, and runs a deterministic investigation. By default, API and worker persist normalized events to the shared SQLite database `data/events.sqlite3` in the repository root. The database is created at first use; if it is empty, existing JSON Lines event files are imported without deleting the originals. Set `Storage__SqlitePath` to move it; use the same path for both processes. The API exposes `GET /api/events?q=...`, `POST /api/ingest`, and `POST /api/investigations` (`{"query":"admin","maxEvents":500}`). Vite development mode (`pnpm dev`) is also available, but this checkout's `C#` directory name contains `#`, which breaks Vite's dependency optimizer; production builds and the ASP.NET dashboard host work normally.

The investigation orchestrator runs Investigation, Detection, Correlation, and Reporting agents in sequence. The first detection rule flags at least three failed authentication events for one user and source IP within ten minutes. Correlations group events by user, source IP, or device. Reports include evidence event IDs; no LLM is called, and Ollama remains unimplemented.

To ingest a file in the background, set `Ingestion__FilePath` and run `dotnet run --project apps/worker/AgenticLogAnalyzer.Worker`. With no file configured the worker reads its small built-in sample. API and worker use the same `Storage__SqlitePath` setting and default database when launched from the repository root. SQLite uses WAL mode so the API and worker can access the development database concurrently.

Set `Storage__Provider=jsonl` to use the legacy JSON Lines repository, with `Storage__FilePath` selecting its file. SQLite is intended for local development; Docker Compose currently starts PostgreSQL for future use, and the application does not persist events to PostgreSQL yet. The investigation flow is deterministic and does not call an LLM.

## Build

```sh
dotnet restore
dotnet build
dotnet test
```

## Security

Never commit real professional logs, credentials, API keys, tokens, or sensitive identifiers. Use anonymized fixtures for tests.
