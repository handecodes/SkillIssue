# Contributing

## Suggesting a new challenge

Challenges are built as platform-maintained forks, not sourced from upstream commit history. The construction and verification model is recorded in [ADR-008](docs/adr/ADR-008.md); read it before suggesting a challenge.

Open a GitHub issue with:

- The GitHub URL of the upstream repo (MIT, Apache 2.0, or BSD licensed; GPL and copyleft are not accepted)
- The bug you have in mind: a small, localized, single-file source defect that a real test already present in the repo catches at current main
- The fully qualified name of that catching test
- A brief note on why the bug is hard to locate: the symptom should not obviously point to the fix location

A good candidate is a behavioral bug a student can grasp (not plumbing, performance, or analyzer noise), fixable in one file, with a misleading symptom that is navigable in three tiered hints. From there the challenge is built as a fork with the bug planted on the default branch, per ADR-008.

## Reporting an app bug

Open a GitHub issue using the bug report template. Include steps to reproduce, what you expected, and what you got.

For security issues, do not use a public issue. See [SECURITY.md](SECURITY.md).

## Development setup

**Prerequisites**

- .NET 10 SDK
- A GitHub OAuth app (create one at github.com/settings/developers)
  - Callback URL: `https://localhost:7239/signin-github`

**Steps**

```bash
git clone https://github.com/handecodes/SkillIssue.git
cd SkillIssue/src/SkillIssue.Web
dotnet user-secrets set "GitHub:ClientId" "<value>"
dotnet user-secrets set "GitHub:ClientSecret" "<value>"
dotnet run --project . --launch-profile https
```

The database is created and seeded on first run.

## Branch strategy

- `main` is production. Do not push directly to main.
- All new work goes on `dev` or a branch off `dev`.
- Open a pull request targeting `dev`. PRs to `main` come from `dev` only.

## Code style

Match the existing patterns. A few things the project avoids:

- No em dashes in any copy (UI strings, error messages, documentation, commit messages)
- No buzzwords in copy ("seamless", "powerful", "robust", "intuitive")
- Comments only where the why is non-obvious; never describe what the code does
- No secrets committed; use `dotnet user-secrets` for credentials and tokens
