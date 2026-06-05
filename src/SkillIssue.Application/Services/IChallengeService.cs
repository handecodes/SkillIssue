using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public interface IChallengeService
{
    Task<IReadOnlyList<Repo>> GetActiveReposWithBugsAsync();
    Task<Bug?> GetBugWithHintsAsync(int bugId);
}
