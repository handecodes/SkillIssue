# Changelog

## [1.2.0] - Unreleased

### Added
- Challenge library expanded to ten challenges across ten distinct domains. Challenges 7 through 10 added: Noda Time (end-of-month clamp), MoreLINQ (quantifier off-by-one), Stateless (superstate-to-substate entry skip), GlobExpressions (single-character wildcard match)
- Lantern favicon (black field, multi-resolution 16/32/48) replacing the placeholder icon
- ADR-008 recording the platform-fork challenge construction model

### Changed
- Challenge sourcing migrated to a platform-fork model: each challenge is a maintained fork (`handecodes/skillissue-*`) with the bug planted on the fork's default branch and a scoped `challenge.yml` that runs only the target test. Students fork, enable Actions, fix on the default branch, and push. This replaces the previous check-out-the-first-commit model. See [ADR-008](docs/adr/ADR-008.md), which supersedes ADR-003
- Removed em dashes from all user-facing text (verifier messages, challenge briefs and hints, instructions)

### Fixed
- Fork verification now scopes to the `challenge.yml` workflow on the fork's default branch instead of the repo-wide latest run. A fork inherits the upstream's own workflows, so a repo-wide query could both falsely pass an unfixed fork and falsely fail a correct one

### Docs
- ADR-003 marked superseded by ADR-008; its body is preserved as a point-in-time record
- Design reference PNGs excluded from version control and the Docker build context

---

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
