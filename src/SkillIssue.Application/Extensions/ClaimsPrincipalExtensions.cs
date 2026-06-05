using System.Security.Claims;

namespace SkillIssue.Application.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("skill_issue:user_id")?.Value, out var id) ? id : null;
}
