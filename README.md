# Skill Issue

Debugging practice on real bugs from real open-source codebases. The kind of work the job actually involves.

Live at **[skillissue.se](https://skillissue.se)**

---

## What it is

You get a failing test, a brief description of the symptom (no location given), and three tiered hints. Fork the repo, find the bug, fix it, push. CI on your fork verifies the result.

Bugs are sourced from real fix commits in MIT/Apache/BSD-licensed .NET libraries. Not generated, not simplified.

---

## Screenshots

_To be added after deploy._

---

## Stack

- C# / .NET 10 LTS
- Blazor Server (interactive SSR over SignalR)
- Entity Framework Core with SQLite
- GitHub OAuth via AspNet.Security.OAuth.GitHub
- Docker (Linux container)
- Azure App Service

---

## Run locally

**Prerequisites**

- .NET 10 SDK
- A GitHub OAuth app ([create one here](https://github.com/settings/developers))
  - Callback URL: `https://localhost:7239/signin-github`

**Setup**

```bash
git clone https://github.com/handecodes/SkillIssue.git
cd SkillIssue
```

Set OAuth credentials via user-secrets. Do not commit these.

```bash
cd src/SkillIssue.Web
dotnet user-secrets set "GitHub:ClientId" "<your-client-id>"
dotnet user-secrets set "GitHub:ClientSecret" "<your-client-secret>"
```

Optionally, set a GitHub PAT to avoid rate limits on fork verification:

```bash
dotnet user-secrets set "GitHub:PatToken" "<your-pat>"
```

**Run**

```bash
dotnet run --project src/SkillIssue.Web --launch-profile https
```

The database is created and seeded on first run. Open `https://localhost:5239`.

**Tests**

```bash
dotnet test
```

**Adding a migration after model changes**

```bash
dotnet ef migrations add <Name> \
  --project src/SkillIssue.Data \
  --startup-project src/SkillIssue.Web
```

---

## Project structure

```
src/
  SkillIssue.Domain/      Entities: User, Repo, Bug, HintTier, FailingTest, Attempt
  SkillIssue.Data/        EF Core DbContext, migrations, seeder
  SkillIssue.Application/ Services, models
  SkillIssue.Web/         Blazor Server app and Program.cs
tests/
  SkillIssue.Tests/       xUnit tests (in-memory SQLite, no external dependencies)
docs/
  adr/                    Architecture decision records
```

Dependency direction: `Web` -> `Application` -> `Data` -> `Domain`

---

## Known limitations

**SQLite write contention.** SQLite handles the current load but will hit contention under concurrent writes. The data layer is structured so a SQL Server migration requires only a connection string and provider swap, with no model changes needed.

**Ephemeral storage in the current deploy.** The SQLite file lives at `/data/skillissue.db` in the container. Azure App Service does not persist this across container restarts without a mounted persistent volume. The database resets on redeploy. Fixing this requires mounting an Azure Files share or switching to Azure SQL.

**No admin UI.** Adding or editing challenges requires a code change and redeployment. There is no web interface for managing the challenge library.

**CSP not implemented.** Blazor Server requires `unsafe-inline` scripts for its SignalR bootstrap. A meaningful Content-Security-Policy needs per-request nonce injection and has been deferred.

**No scale-to-zero handling.** Blazor Server circuits need a warm host. Cold-start latency is noticeable and not handled gracefully.

---

## Bug sources

| Library | License | Repo |
|---------|---------|------|
| Humanizer | MIT | [Humanizr/Humanizer](https://github.com/Humanizr/Humanizer) |
| Newtonsoft.Json | MIT | [JamesNK/Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) |
| Polly | BSD 3-Clause | [App-vNext/Polly](https://github.com/App-vNext/Polly) |
| Serilog | Apache 2.0 | [serilog/serilog](https://github.com/serilog/serilog) |
| NodaTime | Apache 2.0 | [nodatime/nodatime](https://github.com/nodatime/nodatime) |
