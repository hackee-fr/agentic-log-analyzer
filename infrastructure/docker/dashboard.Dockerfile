# syntax=docker/dockerfile:1
# Dashboard image: static React build served by unprivileged nginx on 8080, proxying /api to the API.
# Build from the repository root: docker build -f infrastructure/docker/dashboard.Dockerfile .

FROM --platform=$BUILDPLATFORM node:24-alpine AS build
WORKDIR /web
RUN corepack enable && corepack prepare pnpm@10.29.3 --activate
COPY apps/dashboard/AgenticLogAnalyzer.Dashboard/web/package.json \
     apps/dashboard/AgenticLogAnalyzer.Dashboard/web/pnpm-lock.yaml \
     apps/dashboard/AgenticLogAnalyzer.Dashboard/web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY apps/dashboard/AgenticLogAnalyzer.Dashboard/web/ ./
RUN pnpm build \
 # Same-origin API: the browser calls /api/* on this server and nginx forwards it.
 && printf 'window.__APP_CONFIG__ = { apiBase: "" };\n' > dist/app-config.js

FROM nginxinc/nginx-unprivileged:1-alpine AS runtime
# Pick up Alpine security fixes released after the base image was built, then drop back to the nginx user.
USER root
RUN apk upgrade --no-cache
USER 101
COPY infrastructure/docker/dashboard.nginx.conf /etc/nginx/conf.d/default.conf
COPY infrastructure/docker/dashboard.security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /web/dist /usr/share/nginx/html
EXPOSE 8080
