# Contributing

## Suggesting a new challenge

Open a GitHub issue with:

- The GitHub URL of the repo (MIT, Apache 2.0, or BSD licensed; GPL and copyleft are not accepted)
- The specific PR link that introduced the fix
- The commit SHA of the test-only commit within that PR: the commit that adds the failing test and nothing else. This is a hard requirement. There must be a commit in the PR that touches only test files, with the fix arriving in a later commit.
- The name of the failing test at that test-only commit
- A brief note on why the bug is hard to locate: the symptom should not obviously point to the fix location

Bugs are accepted if they meet the criteria in [ADR-003](docs/adr/ADR-003.md): real fix commit, test-only commit preceding the fix, isolated failing test, misleading symptom, navigable in three tiered hints.

If the PR you want to submit combines the test and fix in a single commit, it does not currently qualify. A platform-fork model is the intended future path for expanding the challenge library beyond this structural constraint. It is not yet implemented.

## Reporting an app bug

Open a GitHub issue using the bug report template. Include steps to reproduce, what you expected, and what you got.

For security issues, do not use a public issue. See [SECURITY.md](SECURITY.md).

## Development setup

**Prerequisites**

- .NET 10 SDK
- A GitHub OAuth app (create one at github.com/settings/developers)
  - Callback URL: `https://localhost:5239/signin-github`

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
