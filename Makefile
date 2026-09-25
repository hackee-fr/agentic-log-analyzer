# Local development commands. Run `make help` for the list.
COMPOSE := docker compose -f infrastructure/docker/docker-compose.yml

.PHONY: help start api front worker stop test

help: ## Show available commands
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F ':.*## ' '{printf "  make %-8s %s\n", $$1, $$2}'

start: ## Start Ollama, the API and the dashboard together (Ctrl+C stops both)
	@$(MAKE) --no-print-directory -j2 api front

api: ## Start Ollama in Docker and the API on :5080 with the LLM enabled (LLM=off to skip)
	@./scripts/dev/api.sh

front: ## Build the dashboard and serve it on :5081
	@./scripts/dev/front.sh

worker: ## Follow a log file: make worker FILE=path/to/app.log
	@test -n "$(FILE)" || (echo "Usage: make worker FILE=path/to/app.log" && exit 1)
	@Ingestion__FilePath="$(abspath $(FILE))" dotnet run --project apps/worker/AgenticLogAnalyzer.Worker

stop: ## Stop the Ollama container (models are kept)
	@$(COMPOSE) stop ollama

test: ## Run the .NET test suite
	@dotnet test
