using System.Security.Claims;

namespace SkillIssue.Application.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("skill_issue:user_id")?.Value, out var id) ? id : null;

    // The GitHub numeric user ID (stable across username renames), set as NameIdentifier at sign-in.
    public static string? GetGitHubId(this ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
