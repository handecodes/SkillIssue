# Changelog

## [1.0.0] — 2026-06-09

First production release, deployed to Azure App Service.

### Added
- Challenge browser with real open-source .NET bug reports sourced from fix-commits
- GitHub OAuth login (AspNet.Security.OAuth.GitHub)
- Challenge detail view: failing test output, repository metadata, difficulty rating
- Submission system: paste a fork URL, the original test suite verifies the fix
- Progress tracking dashboard per user
- IDE-inspired UI: file explorer sidebar, tab bar, editor pane, status bar
- Full-width landing page for logged-out visitors with live bug preview terminal
- Docker multi-stage build targeting `mcr.microsoft.com/dotnet/aspnet:10.0`
- Azure App Service container deployment (ACR-based)

### Security
- CSRF/antiforgery on all state-mutating POST endpoints
- POST-based logout with redirect to prevent open-redirect attacks
- Non-root app user (uid 1654) inside the Docker image
- Secrets via `dotnet user-secrets` — never committed to source
- `AllowedHosts`, forwarded headers, and SQLite path hardened for production
- `.gitignore` excludes `*.db`, `secrets.json`, and other sensitive files
