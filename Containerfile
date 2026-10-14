# ==============================================================================
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
# ==============================================================================
# Diet-Dost Production Multi-Stage Containerfile
# Target: .NET 11 on Linux (Azure Container Apps / App Service / Linux VM)
# Engine: Podman / OCI-compliant container engines
# ==============================================================================

# --- Stage 1: Build & Publish ---
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

# Copy Directory.Build.props
COPY Directory.Build.props ./

# Copy csproj files for optimal layer caching
COPY src/Nutrition.Domain/Nutrition.Domain.csproj src/Nutrition.Domain/
COPY src/Nutrition.Application/Nutrition.Application.csproj src/Nutrition.Application/
COPY src/Nutrition.Infrastructure/Nutrition.Infrastructure.csproj src/Nutrition.Infrastructure/
COPY src/Nutrition.WebGateway/Nutrition.WebGateway.csproj src/Nutrition.WebGateway/

# Restore dependencies
RUN dotnet restore src/Nutrition.WebGateway/Nutrition.WebGateway.csproj

# Copy remaining source code
COPY src/ src/

# Publish Release build
RUN dotnet publish src/Nutrition.WebGateway/Nutrition.WebGateway.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Stage 2: Production Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app

# Create durable mount points for SQLite and static uploads
RUN mkdir -p /app/data /app/data/wwwroot /app/data/backups && \
    chmod -R 777 /app/data

# Copy published application binaries & assets
COPY --from=build /app/publish .

# Environment Defaults for Container Deployments
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=Sqlite
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/diet_dost.db;Cache=Shared"
ENV Storage__WebRootPath="/app/data/wwwroot"
ENV Security__AllowDemoUsers="false"

EXPOSE 8080

ENTRYPOINT ["dotnet", "Nutrition.WebGateway.dll"]
