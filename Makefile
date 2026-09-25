# Local development commands. Run `make help` for the list.
COMPOSE := docker compose -f infrastructure/docker/docker-compose.yml

.PHONY: help start api front desktop desktop-publish desktop-package worker stop test verify prod prod-down

help: ## Show available commands
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F ':.*## ' '{printf "  make %-8s %s\n", $$1, $$2}'

start: ## Start Ollama, the API and the dashboard together (Ctrl+C stops both)
	@$(MAKE) --no-print-directory -j2 api front

api: ## Start Ollama in Docker and the API on :5080 with the LLM enabled (LLM=off to skip)
	@./scripts/dev/api.sh

front: ## Build the dashboard and serve it on :5081
	@./scripts/dev/front.sh

desktop: ## Run the desktop app (native window, API included)
	@./scripts/dev/desktop.sh

desktop-publish: ## Build a standalone desktop app: make desktop-publish [RID=osx-arm64|osx-x64|win-x64|linux-x64]
	@RID="$(RID)" ./scripts/dev/desktop-publish.sh

desktop-package: ## Build the Velopack installer and update feed for RID: make desktop-package [RID=…] [VERSION=…]
	@RID="$(RID)" VERSION="$(VERSION)" ./scripts/dev/desktop-package.sh

worker: ## Follow a log file: make worker FILE=path/to/app.log
	@test -n "$(FILE)" || (echo "Usage: make worker FILE=path/to/app.log" && exit 1)
	@Ingestion__FilePath="$(abspath $(FILE))" dotnet run --project apps/worker/AgenticLogAnalyzer.Worker

prod: ## Build and start the production stack in Docker (dashboard on :8080, API and Ollama internal)
	@docker compose -f infrastructure/docker/docker-compose.prod.yml up -d --build --wait

prod-down: ## Stop the production stack (volumes are kept)
	@docker compose -f infrastructure/docker/docker-compose.prod.yml down

stop: ## Stop the Ollama container (models are kept)
	@$(COMPOSE) stop ollama

test: ## Run the .NET test suite
	@dotnet test

verify: ## Run the same frontend and .NET checks as CI
	@cd apps/dashboard/AgenticLogAnalyzer.Dashboard/web && pnpm install --frozen-lockfile && pnpm lint && pnpm build
	@dotnet restore AgenticLogAnalyzer.slnx
	@dotnet format AgenticLogAnalyzer.slnx --verify-no-changes --no-restore
	@dotnet build AgenticLogAnalyzer.slnx --configuration Release --no-restore
	@dotnet test AgenticLogAnalyzer.slnx --configuration Release --no-build --no-restore
