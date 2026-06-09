# ── Stage 1: build & publish ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first. dotnet restore runs in its own layer so
# Docker can cache it — a source-code change won't re-download all NuGet
# packages; only a .csproj change will bust this layer.
COPY src/SkillIssue.Domain/SkillIssue.Domain.csproj           src/SkillIssue.Domain/
COPY src/SkillIssue.Data/SkillIssue.Data.csproj               src/SkillIssue.Data/
COPY src/SkillIssue.Application/SkillIssue.Application.csproj src/SkillIssue.Application/
COPY src/SkillIssue.Web/SkillIssue.Web.csproj                 src/SkillIssue.Web/

RUN dotnet restore src/SkillIssue.Web/SkillIssue.Web.csproj

# Now copy the full source and publish. This layer busts on any source change
# but not on dependency changes, so restore stays cached in the layer above.
COPY src/ src/

RUN dotnet publish src/SkillIssue.Web/SkillIssue.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish

# ── Stage 2: runtime-only image ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create the SQLite mount point and hand ownership to the non-root app user
# before we switch to that user — root is the only account that can chown.
RUN mkdir /data && chown app:app /data

# Copy only the published output from the build stage. The SDK, compiler,
# source files, and NuGet cache from stage 1 are discarded entirely.
COPY --from=build /app/publish .

# ASPNETCORE_HTTP_PORTS is already 8080 in the base image; we set it
# explicitly here so the value is visible to anyone reading this file.
# Override ConnectionStrings__DefaultConnection at deploy time to point
# at a persistent volume path (e.g. /data/skillissue.db on a mounted disk).
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="Data Source=/data/skillissue.db"

# Document which port the app listens on. Set WEBSITES_PORT=8080 in the
# Azure App Service configuration panel to match.
EXPOSE 8080

# Run as the non-root app user (uid 1654) defined by the base image.
# The /app directory is world-readable; only /data needs write access.
USER app

ENTRYPOINT ["dotnet", "SkillIssue.Web.dll"]
