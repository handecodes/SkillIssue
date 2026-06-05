namespace SkillIssue.Application.Models;

public record RepoProgress(int RepoId, string RepoName, int TotalBugs, int Solved, int HintsUsed);

public record UserProgressSummary(int TotalAttempts, int UniqueSolved, IReadOnlyList<RepoProgress> ByRepo);
