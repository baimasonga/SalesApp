# Salone Sales — production container
# Multi-stage build, non-root runtime user, baked-in healthcheck.

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for layer caching
COPY *.csproj ./
RUN dotnet restore

# Copy source and publish
COPY . ./
ARG COMMIT_SHA=dev
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for the runtime
RUN groupadd -r salone && useradd -r -g salone -d /app -s /sbin/nologin salone

COPY --from=build --chown=salone:salone /app .

# Writable logs dir
RUN mkdir -p /app/logs && chown -R salone:salone /app/logs

USER salone

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ARG COMMIT_SHA=dev
ENV COMMIT_SHA=${COMMIT_SHA}

EXPOSE 8080

# Container-level health check the orchestrator can poll
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "SalesApp.dll"]
