# Agentic Log Analyzer

Extensible log ingestion, normalization, search, detection, correlation, and agent-assisted investigation platform built with C#/.NET.

## Architecture

`Sources → Connectors → Parsing → Normalization → Storage → Detection/Search → Correlation → Tools → Skills → Agents → LLM → Investigation → Report`

The deterministic engine works without an LLM. The initial target is PostgreSQL for storage and Ollama for local LLM inference.

## Monorepo

- `apps/api` — ASP.NET Core API
- `apps/worker` — background ingestion/processing worker
- `apps/dashboard` — web dashboard
- `apps/desktop` — cross-platform desktop app (Photino) embedding the API and dashboard
- `packages/domain` — domain model
- `packages/application` — use cases and abstractions
- `packages/infrastructure` — persistence and external infrastructure
- `packages/connectors` — log source connectors
- `packages/detection` — deterministic detections
- `packages/correlation` — event correlation
- `packages/agentic` — tools, skills, and agent orchestration
- `packages/llm` — LLM provider implementations
- `packages/hosting` — API services and routes shared by the web API and the desktop app
- `tests` — unit, integration, and end-to-end tests
- `knowledge` — source and protocol knowledge
- `infrastructure` — Docker, PostgreSQL, and Hyper-V lab assets

## Run locally

Quick start (needs the .NET 10 SDK, pnpm and Docker Desktop for the LLM):

```sh
make start   # Ollama in Docker + API on :5080 (LLM enabled) + dashboard on :5081
```

Or in two terminals: `make api` and `make front`. `make api` starts Ollama, downloads the model on first run and enables the LLM; use `LLM=off make api` to skip it. Each command stops a previous instance of the app still holding its port. `make help` lists the other commands (`worker`, `stop`, `test`).

Manual steps:


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

Open `http://localhost:5081`. The dashboard imports `.log` and `.txt` files or pasted lines, searches events, and runs a deterministic investigation. Its Settings page reads runtime status from `GET /api/settings`. By default, API and worker persist normalized events to the shared SQLite database `data/events.sqlite3` in the repository root. The database is created at first use; if it is empty, existing JSON Lines event files are imported without deleting the originals. Set `Storage__SqlitePath` to move it; use the same path for both processes. Other API routes include `GET /api/events?q=...`, `POST /api/ingest`, and `POST /api/investigations` (`{"query":"admin","maxEvents":500}`), and `DELETE /api/events?source=<name>` or `DELETE /api/events?all=true`, which the Sources page uses to delete imported data. Deletion is permanent, and the one-time JSON Lines import does not run again after a database has been emptied. Vite development mode (`pnpm dev`) is also available, but this checkout's `C#` directory name contains `#`, which breaks Vite's dependency optimizer; production builds and the ASP.NET dashboard host work normally.

The investigation orchestrator runs Investigation, Detection, Correlation, and Reporting agents in sequence. The first detection rule flags at least three failed authentication events for one user and source IP within ten minutes. Correlations group events by user, source IP, or device. Reports include evidence event IDs; the investigation flow never calls an LLM.

To ingest a file in the background, set `Ingestion__FilePath` and run `dotnet run --project apps/worker/AgenticLogAnalyzer.Worker`. The worker reads the file, then follows appended lines like `tail -f` (poll interval `Ingestion__PollIntervalMs`, default 1000); it waits for a missing file and restarts from the beginning when the file is truncated. Set `Ingestion__Follow=false` to read the file once. Restarting the worker re-reads the file, but deterministic event IDs prevent duplicates. With no file configured the worker ingests nothing; set `Ingestion__UseSample=true` to load its two demo lines. API and worker use the same `Storage__SqlitePath` setting and default database when launched from the repository root. SQLite uses WAL mode so the API and worker can access the development database concurrently.

Set `Storage__Provider=jsonl` to use the legacy JSON Lines repository, with `Storage__FilePath` selecting its file. SQLite is intended for local development; Docker Compose currently starts PostgreSQL for future use, and the application does not persist events to PostgreSQL yet. The investigation flow is deterministic and does not call an LLM.

## Desktop app (Windows, macOS, Linux)

`apps/desktop` packages the same API and dashboard as a native window ([Photino](https://www.tryphotino.io)): the API runs in-process on a random loopback port and the built dashboard is served from the same origin. The web setup above stays the development workflow.

```sh
make desktop                           # build the dashboard and open the desktop app
make desktop-publish                   # standalone build for this machine (no .NET needed to run it)
make desktop-publish RID=win-x64       # or osx-arm64, osx-x64, linux-x64
```

Output goes to `artifacts/desktop/<rid>/`; macOS targets produce `Agentic Log Analyzer.app`. Data is stored per user in `%LOCALAPPDATA%\AgenticLogAnalyzer` (Windows), `~/Library/Application Support/AgenticLogAnalyzer` (macOS) or `~/.local/share/AgenticLogAnalyzer` (Linux). The LLM is enabled by default and uses Ollama on `http://localhost:11434` (native install or the Docker service below); without it the assistant keeps its deterministic answers. The same `Storage__*` and `Llm__*` environment variables apply.

`make desktop-package [RID=…] [VERSION=…]` also wraps the build in `artifacts/packages/`: a `.dmg` on macOS, a `.zip` plus an Inno Setup `-setup.exe` on Windows (when `iscc` is installed), and a `.tar.gz` plus an AppImage on Linux (when `appimagetool` is installed).

Releases: the `Desktop packages` GitHub Actions workflow builds all four targets (`osx-arm64`, `osx-x64`, `win-x64`, `linux-x64`) on native runners. Pushing a tag publishes them as a GitHub Release:

```sh
git tag v0.3.0 && git push origin v0.3.0
```

Running the workflow manually (Actions › Desktop packages › Run workflow) only uploads the packages as build artifacts.

Builds are not signed yet: macOS asks to confirm the first launch (right-click › Open), Windows SmartScreen may warn, and Linux needs WebKitGTK (`libwebkit2gtk-4.1`).

## Local LLM with Ollama (Docker)

The dashboard Assistant (`POST /api/chat`) always computes its answer deterministically. When an LLM is enabled, Ollama only rephrases that answer; the rewording is rejected, and the deterministic answer shown instead, if it cites an IP address absent from the facts or denies a threat while a detection fired. Every answer keeps its evidence events.

Start Ollama and download the model (about 2 GB, once; models persist in the `ollama_data` volume):

```sh
docker compose -f infrastructure/docker/docker-compose.yml up -d ollama ollama-pull
```

Then start the API with the provider enabled:

```sh
Llm__Provider=ollama dotnet run --project apps/api/AgenticLogAnalyzer.Api -- --urls http://localhost:5080
```

Optional settings: `Llm__Ollama__BaseUrl` (default `http://localhost:11434`), `Llm__Ollama__Model` (default `llama3.2`; pull other models with `OLLAMA_MODEL=<name>` on the compose command), `Llm__Ollama__TimeoutSeconds` (180), `Llm__Ollama__MaxOutputTokens` (350) and `Llm__Ollama__KeepAlive` (`15m`). `GET /api/llm/status` and the Settings page report whether Ollama is reachable and the model is installed. Ollama is published on `127.0.0.1` only.

Docker Desktop on macOS cannot use the Apple GPU, so inference runs on CPU: expect roughly 15–70 seconds per answer with `llama3.2`. A native Ollama install (`brew install ollama`) uses Metal and is much faster, and works with the same settings.

To run the opt-in integration tests against a running Ollama:

```sh
OLLAMA_INTEGRATION_URL=http://localhost:11434 dotnet test --filter OllamaIntegrationTests
```

## Container images and deployment

`infrastructure/docker/api.Dockerfile` (chiseled ASP.NET runtime, non-root) and `dashboard.Dockerfile` (unprivileged nginx serving the build and proxying `/api` to the API) produce the web images. `docker-compose.prod.yml` runs the dashboard, API and Ollama with persistent volumes; only the dashboard is published, on `127.0.0.1:8080` by default (`DASHBOARD_BIND=0.0.0.0` exposes it on the network — there is no authentication yet).

```sh
make prod        # build the images locally and start the stack
make prod-down   # stop it (data and models are kept)
```

The `Docker images` workflow audits NuGet and npm dependencies, builds both images and fails on fixable HIGH/CRITICAL vulnerabilities (Trivy). On `main` it pushes `ghcr.io/hackee-fr/agentic-log-analyzer-{api,dashboard}` as `:latest`, `:main` and `:sha-<commit>`; a `v*` tag adds `:<version>` and `:<major>.<minor>`. To run published images on a server:

```sh
IMAGE_TAG=0.3.0 docker compose -f infrastructure/docker/docker-compose.prod.yml pull
IMAGE_TAG=0.3.0 docker compose -f infrastructure/docker/docker-compose.prod.yml up -d
```

## Build

Run the same frontend and .NET checks as GitHub Actions with:

```sh
make verify
```

The CI workflow runs on pushes and pull requests targeting `main`; it also validates the local shell scripts and Docker Compose configuration.

```sh
dotnet restore
dotnet build
dotnet test
```

## Security

Never commit real professional logs, credentials, API keys, tokens, or sensitive identifiers. Use anonymized fixtures for tests.
