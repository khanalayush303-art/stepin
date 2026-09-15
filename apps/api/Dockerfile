# syntax=docker/dockerfile:1

# --------------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so `restore` is cached until a csproj changes.
COPY Directory.Build.props ./
COPY src/StepIn.Domain/StepIn.Domain.csproj src/StepIn.Domain/
COPY src/StepIn.Application/StepIn.Application.csproj src/StepIn.Application/
COPY src/StepIn.Infrastructure/StepIn.Infrastructure.csproj src/StepIn.Infrastructure/
COPY src/StepIn.Api/StepIn.Api.csproj src/StepIn.Api/
RUN dotnet restore src/StepIn.Api/StepIn.Api.csproj

COPY . .
RUN dotnet publish src/StepIn.Api/StepIn.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# ----------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Runs as the non-root `app` user that the aspnet image ships with.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StepIn.Api.dll"]
