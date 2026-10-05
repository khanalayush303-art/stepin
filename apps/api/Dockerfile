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

# Git commit this image was built from. Pass it at build time:
#   fly deploy --build-arg GIT_SHA=$(git rev-parse HEAD)
# so the deployed image can be matched to source. Defaults to "unknown" for builds that omit it.
ARG GIT_SHA=unknown
LABEL org.opencontainers.image.revision=$GIT_SHA

# COPY below always writes root:root regardless of the active USER, so this
# directory needs its ownership fixed up while still root, before the switch.
RUN mkdir -p /app/data/resumes && chown -R app:app /app/data

# Runs as the non-root `app` user that the aspnet image ships with.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StepIn.Api.dll"]
