using Microsoft.EntityFrameworkCore;
using SkillIssue.Data;
using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public class ChallengeService(IDbContextFactory<AppDbContext> factory) : IChallengeService
{
    public async Task<IReadOnlyList<Repo>> GetActiveReposWithBugsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Repos
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Include(r => r.Bugs)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<Bug?> GetBugWithHintsAsync(int bugId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Bugs
            .AsNoTracking()
            .Include(b => b.Repo)
            .Include(b => b.Hints.OrderBy(h => h.Order))
            .FirstOrDefaultAsync(b => b.Id == bugId);
    }
}
