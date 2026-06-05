using SkillIssue.Application.Models;
using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public interface IAttemptService
{
    Task<Attempt> RecordAttemptAsync(int userId, int bugId, string forkUrl, bool passed, int hintsUsed, int? elapsedSeconds);
    Task<UserProgressSummary> GetProgressSummaryAsync(int userId);
}
