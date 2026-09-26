# ==============================================================================
# Diet-Dost Production Multi-Stage Containerfile
# Target: .NET 11 on Linux (Azure Container Apps / App Service / Linux VM)
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
RUN mkdir -p /app/data /app/data/wwwroot /app/data/backups

# Copy published application binaries & assets
COPY --from=build /app/publish .

# Environment Defaults for Container Deployments
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=Sqlite
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/diet_dost.db;Cache=Shared"
ENV Storage__WebRootPath="/app/data/wwwroot"

EXPOSE 8080

ENTRYPOINT ["dotnet", "Nutrition.WebGateway.dll"]
