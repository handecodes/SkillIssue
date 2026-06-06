using Microsoft.EntityFrameworkCore;
using SkillIssue.Application.Models;
using SkillIssue.Data;
using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public class AttemptService(IDbContextFactory<AppDbContext> factory) : IAttemptService
{
    public async Task<Attempt> RecordAttemptAsync(
        int userId, int bugId, string forkUrl, bool passed, int hintsUsed, int? elapsedSeconds)
    {
        await using var db = await factory.CreateDbContextAsync();

        var attempt = new Attempt
        {
            UserId = userId,
            BugId = bugId,
            ForkUrl = forkUrl,
            Passed = passed,
            HintsUsed = hintsUsed,
            ElapsedSeconds = elapsedSeconds,
            SubmittedAt = DateTime.UtcNow
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt;
    }

    public async Task<UserProgressSummary> GetProgressSummaryAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var attempts = await db.Attempts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Bug)
                .ThenInclude(b => b.Repo)
            .ToListAsync();

        var totalAttempts = attempts.Count;
        var uniqueSolved = attempts
            .Where(a => a.Passed)
            .Select(a => a.BugId)
            .Distinct()
            .Count();

        var repos = await db.Repos
            .AsNoTracking()
            .Include(r => r.Bugs)
            .Where(r => r.IsActive)
            .ToListAsync();

        var solvedBugIds = attempts
            .Where(a => a.Passed)
            .Select(a => a.BugId)
            .ToHashSet();

        var byRepo = repos.Select(repo =>
        {
            var repoBugIds = repo.Bugs.Select(b => b.Id).ToHashSet();
            var repoAttempts = attempts.Where(a => repoBugIds.Contains(a.BugId)).ToList();
            // HintsUsed is cumulative across all attempts, not just the final one.
            // Two attempts using 2 and 1 hints respectively → HintsUsed = 3.
            return new RepoProgress(
                RepoId: repo.Id,
                RepoName: repo.Name,
                TotalBugs: repo.Bugs.Count,
                Solved: repoBugIds.Count(id => solvedBugIds.Contains(id)),
                HintsUsed: repoAttempts.Sum(a => a.HintsUsed)
            );
        }).ToList();

        return new UserProgressSummary(totalAttempts, uniqueSolved, byRepo);
    }
}
