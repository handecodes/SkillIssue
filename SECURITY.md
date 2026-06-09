# Security Policy

## Reporting a vulnerability

Do not open a public issue for security vulnerabilities.

Use [GitHub's private vulnerability reporting](https://github.com/handecodes/SkillIssue/security/advisories/new) to submit a report confidentially. This keeps the details private until a fix is in place.

We will acknowledge receipt within 72 hours and follow up with an assessment of severity and timeline.

## In scope

- Authentication and session handling (GitHub OAuth flow, cookie security)
- Authorization checks (route protection, per-user data access)
- Injection vulnerabilities in the app or its GitHub API calls
- User data exposure (accounts, attempt history, progress)
- Rate limiting bypass on fork submission or login

## Out of scope

The challenge repos themselves (Humanizer, Polly, Serilog, NodaTime, Newtonsoft.Json) are external open-source projects maintained by their own teams. Vulnerabilities in those repos should be reported to their maintainers, not here.

Findings that require physical access to the server, social engineering, or are limited to self-XSS are out of scope.
