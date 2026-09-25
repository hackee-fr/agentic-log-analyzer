# Agentic Log Analyzer — Development Contract

## Mission

Build an extensible log analysis platform in C#/.NET. The deterministic log engine is the source of truth; LLMs are optional reasoning components.

## Architecture

Dependency direction:

Domain
  ↓
Application
  ↓
Infrastructure / Connectors / Detection / Correlation / Agentic / Llm
  ↓
API / Worker

The Domain project must not reference Infrastructure, databases, HTTP clients, LLM SDKs, or UI.

## Rules

1. Keep the canonical event model source-agnostic.
2. Never invent vendor-specific log formats. Use real anonymized fixtures.
3. Keep parsing, normalization, detection, correlation, and reasoning separate.
4. Agent tools must expose deterministic facts to agents.
5. LLM output is never treated as authoritative without verification.
6. Never commit credentials, tokens, customer data, or production logs.
7. Add tests for parsing, normalization, detection, and correlation behavior.
8. Prefer small composable services over large service classes.
9. Use async APIs for I/O and propagate CancellationToken.
10. Keep public APIs documented when behavior is non-obvious.

## Local commands

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `dotnet format --verify-no-changes`

## Delivery order

1. Raw log ingestion
2. Parsing
3. Canonical event normalization
4. PostgreSQL persistence
5. Search
6. Timeline/correlation
7. Detection
8. Tools and skills
9. Agents
10. Ollama/LLM integration
11. API/dashboard
12. Docker and CI/CD

Do not add agentic complexity before the deterministic engine works.
