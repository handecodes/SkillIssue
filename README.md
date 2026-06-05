# Skill Issue

A debugging trainer that teaches students how to navigate large, real codebases. Students pick a challenge (a real bug planted in a .NET repo), locate and fix it in their own IDE, push to their fork, and the app verifies the fix via GitHub Actions CI.

## Architecture

```
src/
  SkillIssue.Domain/       — Entities: User, Repo, Bug, HintTier, Attempt
  SkillIssue.Data/         — EF Core DbContext, migrations, seeder
  SkillIssue.Application/  — Services, models, business logic
  SkillIssue.Web/          — Blazor Server frontend + Program.cs
tests/
  SkillIssue.Tests/        — xUnit tests for services
```

Dependency flow: `Web` → `Application` → `Data` → `Domain`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [GitHub OAuth App](https://github.com/settings/applications/new)
  - Homepage URL: `http://localhost:5239`
  - Callback URL: `http://localhost:5239/signin-github`

## Setup

### 1. Clone and restore

```bash
git clone <repo-url>
cd SkillIssue
dotnet restore
```

### 2. Configure GitHub OAuth credentials

Use .NET user-secrets (never commit credentials):

```bash
cd src/SkillIssue.Web
dotnet user-secrets set "GitHub:ClientId" "<your-client-id>"
dotnet user-secrets set "GitHub:ClientSecret" "<your-client-secret>"
```

### 3. Apply database migrations

Migrations are applied automatically on first run. To create new migrations after model changes:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/SkillIssue.Data \
  --startup-project src/SkillIssue.Web
```

### 4. Run

```bash
dotnet run --project src/SkillIssue.Web
```

The app opens at `http://localhost:5239`. The database (`skillissue.db`) is created automatically with seed data (2 sample repos, 3 challenges).

## Running tests

```bash
dotnet test
```

Tests use an in-memory SQLite database — no setup required.

## Core flow

1. **Browse** — Home page lists all active repos and their bugs, sorted by difficulty
2. **Challenge** — Click a challenge to see the brief, error message, and failing tests
3. **Hints** — Reveal up to 3 tiered hints (nudge → area → file & line)
4. **Fix** — Fork the repo on GitHub, fix the bug in your IDE, push
5. **Submit** — Paste your fork URL; the app checks your latest CI run via GitHub API
6. **Progress** — `/progress` shows solved challenges and hint usage per repo

## Database

Uses SQLite in development (file: `skillissue.db` in the Web project output directory). To switch to SQL Server for production, change the provider in `ServiceCollectionExtensions` in `SkillIssue.Data` and update the connection string in `appsettings.json`.

## Seed data

Two repos seeded on first run:

| Repo | Bug | Difficulty |
|------|-----|-----------|
| MathUtil | IsPrime returns true for 1 | Easy |
| OrderProcessor | Discount not applied to orders of exactly $500 | Medium |
| OrderProcessor | Zero-quantity items inflate the order total | Hard |
