# Changelog

## [1.1.0] - 2026-06-11

### Added
- Challenge library expanded to 9 structurally validated challenges across 6 repositories (Humanizer, Castle.Core, NUnit, Polly, Autofac, Newtonsoft.Json)
- Hard difficulty tier: challenges now span Easy, Medium, and Hard
- Rate limiting on fork verification: sliding window per user prevents abuse
- "How challenges are sourced" section in README explaining the test-first commit requirement and difficulty definition
- Known limitation documented: challenge sourcing requires a test-only commit preceding the fix commit in the PR

### Changed
- Landing page redesigned with IDE aesthetic and wine theme; decorative tab bar added to the editor chrome
- Font swapped to Commit Mono across the application
- All challenge briefs rewritten: symptom-only descriptions with no location hints, no em dashes
- `ForwardedHeaders` middleware configured to trust `X-Forwarded-Proto` from the Azure reverse proxy (fixes HTTPS scheme detection behind the load balancer)
- Bug sources table updated to reflect current seeder; removed Serilog and NodaTime which are not in the seeder

### Fixed
- Null `AuthState` crash paths that caused unhandled exceptions on circuit reconnect
- Version string rendering in the status bar
- Auth hardening: `[Authorize]` attribute applied consistently; logout uses POST to prevent open-redirect
- Security headers added in production middleware pipeline

### Docs
- Full documentation pass: README, CHANGELOG, all ADRs, CONTRIBUTING updated to match current project state
- ADR-003 expanded with structural sourcing requirement and platform-fork model note
- CONTRIBUTING updated with test-first commit requirement for challenge submissions

---

## [1.0.0] - 2026-06-09

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
- Secrets via `dotnet user-secrets`; never committed to source
- `AllowedHosts`, forwarded headers, and SQLite path hardened for production
- `.gitignore` excludes `*.db`, `secrets.json`, and other sensitive files
