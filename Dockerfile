# Multi-stage Dockerfile for Keydral API

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder

WORKDIR /src

# Copy solution and project files
COPY ["Keydral.sln", "./"]
COPY ["src/Keydral.API/Keydral.API.csproj", "src/Keydral.API/"]
COPY ["src/Keydral.Core/Keydral.Core.csproj", "src/Keydral.Core/"]
COPY ["src/Keydral.Encryption/Keydral.Encryption.csproj", "src/Keydral.Encryption/"]
COPY ["src/Keydral.Storage/Keydral.Storage.csproj", "src/Keydral.Storage/"]

# Restore dependencies
RUN dotnet restore "Keydral.sln"

# Copy source code
COPY ["src/", "src/"]

# Build
RUN dotnet build "Keydral.sln" -c Release --no-restore

# Publish
RUN dotnet publish "src/Keydral.API/Keydral.API.csproj" -c Release -o /app/publish --no-build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

WORKDIR /app

# Copy published app
COPY --from=builder /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Run API
ENTRYPOINT ["dotnet", "Keydral.API.dll"]
