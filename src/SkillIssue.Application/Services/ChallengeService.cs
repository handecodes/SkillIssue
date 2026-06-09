using Microsoft.EntityFrameworkCore;
using SkillIssue.Data;
using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public class ChallengeService(IDbContextFactory<AppDbContext> factory) : IChallengeService
{
    // Returns Domain entities directly as view data. Safe because all queries use AsNoTracking()
    // and lazy loading is disabled, so no tracking leakage or N+1 risk. A DTO layer is not
    // warranted at current scale, but these return types couple the web layer to the persistence model.
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
            .Include(b => b.FailingTests.OrderBy(f => f.Order))
            .FirstOrDefaultAsync(b => b.Id == bugId);
    }
}
