# syntax=docker/dockerfile:1
# API image: ASP.NET Core on a chiseled (distroless) runtime, non-root, listening on 8080.
# Build from the repository root: docker build -f infrastructure/docker/api.Dockerfile .

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY packages/ packages/
COPY apps/api/ apps/api/
RUN dotnet restore apps/api/AgenticLogAnalyzer.Api/AgenticLogAnalyzer.Api.csproj -a "$TARGETARCH"
RUN dotnet publish apps/api/AgenticLogAnalyzer.Api/AgenticLogAnalyzer.Api.csproj \
      -c Release -a "$TARGETARCH" --no-restore -p:Version="$VERSION" -p:DebugType=none -o /app \
 && mkdir -p /data && chown 1654:1654 /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --from=build --chown=1654:1654 /data /data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__Provider=sqlite \
    Storage__SqlitePath=/data/events.sqlite3 \
    DOTNET_gcServer=0
VOLUME ["/data"]
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "AgenticLogAnalyzer.Api.dll"]
