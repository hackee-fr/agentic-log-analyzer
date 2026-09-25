# Changelog

All notable changes to Agentic Log Analyzer are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).
Desktop installers and Docker images are published from `v*` tags (see the README).

## [Unreleased]

## [0.4.0] - 2026-09-25

### Added

- **Desktop auto-update** (Velopack): installed apps check GitHub Releases at startup and every six hours, download new versions in the background, and install them on "Restart now" or when the app closes. Delta packages keep updates small. Settings shows the installed version and has a manual check.

### Changed

- Desktop installers are built with Velopack: `-Setup.exe` (Windows), `-Setup.pkg` (macOS), `.AppImage` (Linux) and portable `.zip` files, plus the update feed. They replace the Inno Setup installer, the `.dmg` and the `.tar.gz`. Apps installed from 0.3.0 must be reinstalled once to receive updates.

## [0.3.0] - 2026-09-25

### Added

- **Log assistant**: `POST /api/chat` and a dashboard Assistant view answer plain-language questions (summary, security, source IPs, users, timeline, keyword search) from stored events and deterministic detections. Every answer lists its evidence events.
- **Ollama integration**: optional local LLM that rephrases the deterministic answer. It runs in Docker (`ollama` and `ollama-pull` services), and `GET /api/llm/status` plus the Settings page report whether Ollama is reachable and the model installed. The timeout, keep-alive and output-token limit are configurable.
- **LLM answer verification**: a rewording is rejected, and the deterministic answer shown instead, when it cites an IP address absent from the facts or denies a threat while a detection fired.
- **SQLite storage** (default) shared by the API and the worker. Existing JSON Lines files are imported once. `Storage__Provider=jsonl` keeps the previous repository.
- **Data deletion**: `DELETE /api/events?source=<name>` and `?all=true`, available from the Sources view with confirmation.
- **Worker file following**: `TailingFileConnector` reads a file and then its appended lines (like `tail -f`). It never splits partially written lines, waits for a missing file and restarts after truncation.
- **Dashboard**:
  - onboarding panel, clickable stat cards and an adaptive activity histogram by outcome;
  - event detail dialog (copy, filter by entity), outcome filters, and clickable evidence and correlated entities;
  - Settings page, active view kept in the URL, `/` keyboard shortcut, loading skeletons and a dark blue theme.
- **Desktop app** for Windows, macOS and Linux (Photino). It runs the same API in-process and serves the dashboard in a native window. Data is stored in the per-user application data folder.
- **Desktop packaging**: `.dmg` (macOS), `-setup.exe` and `.zip` (Windows, Inno Setup), AppImage and `.tar.gz` (Linux), built by the `Desktop packages` workflow and attached to a GitHub Release on `v*` tags.
- **Container images**: API (chiseled .NET runtime, non-root) and dashboard (unprivileged nginx proxying `/api`), plus `docker-compose.prod.yml` for the whole stack.
- **CI/CD**:
  - `CI` workflow running `make verify`;
  - `Docker images` workflow: dependency audit, Trivy scan, then multi-arch push to `ghcr.io` from `main` and `v*` tags.
- **Automatic releases**: when `<Version>` in `Directory.Build.props` changes on `main` and `CHANGELOG.md` has a matching section, the `Release` workflow verifies the code, then creates the `vX.Y.Z` tag, the GitHub Release (installers and notes from this changelog) and the `:X.Y.Z` Docker images.
- **`make` commands**: `start`, `api`, `front`, `desktop`, `desktop-publish`, `desktop-package`, `prod`, `prod-down`, `worker`, `stop`, `test` and `verify`.

### Changed

- The product version is declared once, in `Directory.Build.props`; `/api/info`, the installers and the images use it.
- The API services and routes moved to `packages/hosting`, shared by the web API and the desktop app.
- The worker no longer writes its demo lines into the shared database unless `Ingestion__UseSample=true` is set.
- The dashboard reads its API address from `app-config.js`: the same origin in the desktop app and the container stack, `http://localhost:5080` in the web development setup.

### Fixed

- Re-importing a file no longer duplicates events: event IDs are now deterministic (UUIDv8 derived from the source and the raw line).
- Emptying the SQLite database no longer re-imports legacy JSON Lines events on the next start.
- Tailwind text sizes now apply to buttons and inputs: an unlayered `font: inherit` rule was overriding them.
- The API image build no longer fails in CI: the `VERSION` build argument, exposed to MSBuild as an environment variable, set an invalid `Version` (e.g. `main`) that made `dotnet restore` fail without an error message. It is now `APP_VERSION`, and it only carries a valid version number.

### Known issues

- Desktop builds are not code-signed: macOS asks for confirmation on first launch, and Windows SmartScreen may warn.
- The desktop app does not bundle the worker. Ollama is used only when it is already running on `localhost:11434`.
- The API has no authentication. The containers publish the dashboard on `127.0.0.1` only by default.

## [0.2.0] - 2026-09-25

### Added

- Deterministic parser for pipe-delimited lines and `timestamp LEVEL [component] message` lines with `key=value` attributes.
- File and sample connectors, a worker ingestion pipeline, and a JSON Lines event repository.
- Investigation orchestrator with deterministic Investigation, Detection, Correlation and Reporting agents. Reports separate facts, hypotheses and recommendations, each with evidence event IDs.
- Detection rule `AUTH-001`: three or more failed authentications for one user and source IP within ten minutes.
- Correlation of events sharing a user, source IP or device.
- HTTP API: `/health`, `/api/info`, `GET /api/events`, `POST /api/ingest`, `POST /api/investigations`.
- React dashboard (Tailwind CSS, shadcn/ui) with overview, events, sources and investigations, served by an ASP.NET host.
- xUnit tests for parsing, the connectors, the repository and the orchestrator.

## [0.1.0] - 2026-09-25

### Added

- .NET 10 monorepo: solution, shared MSBuild settings, central package management and editorconfig.
- Domain model (`RawLog`, `CanonicalEvent`) and application abstractions: connector, parser, repository and LLM provider.
- Projects for infrastructure, connectors, detection, correlation, agentic, LLM, API, worker and dashboard.
- PostgreSQL development service in Docker Compose.
- Requirements document, development contract (`AGENTS.md`), and agent and skill contracts.

[Unreleased]: https://github.com/hackee-fr/agentic-log-analyzer/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/hackee-fr/agentic-log-analyzer/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/hackee-fr/agentic-log-analyzer/compare/cd61555...v0.3.0
[0.2.0]: https://github.com/hackee-fr/agentic-log-analyzer/compare/248368c...cd61555
[0.1.0]: https://github.com/hackee-fr/agentic-log-analyzer/commit/248368c
