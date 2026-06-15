using Microsoft.EntityFrameworkCore;
using SkillIssue.Data.Seeding;
using SkillIssue.Domain;
using SkillIssue.Tests.Helpers;

namespace SkillIssue.Tests;

public class DbSeederTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();

    [Fact]
    public async Task SeedAsync_SecondCallOnAlreadySeededDb_IsNoOp_AndPreservesAttempts()
    {
        // First seed populates the current challenge set.
        await using (var db = await _factory.CreateDbContextAsync())
            await DbSeeder.SeedAsync(db);

        int repoCount;
        int seededBugId;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            repoCount = await db.Repos.CountAsync();
            seededBugId = await db.Bugs.Select(b => b.Id).FirstAsync();
        }
        Assert.Equal(10, repoCount); // sanity: the 10 active challenges seeded

        // Simulate real user progress: a recorded attempt against a seeded bug.
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var user = new User { GitHubId = "1", Login = "alice", DisplayName = "Alice" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Attempts.Add(new Attempt
            {
                UserId = user.Id,
                BugId = seededBugId,
                ForkUrl = "https://github.com/alice/fork",
                Passed = true,
                SubmittedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Second seed on the already-seeded DB must short-circuit: no destructive reseed.
        await using (var db = await _factory.CreateDbContextAsync())
            await DbSeeder.SeedAsync(db);

        await using var verify = await _factory.CreateDbContextAsync();
        // The challenge set was neither wiped+reinserted nor duplicated.
        Assert.Equal(10, await verify.Repos.CountAsync());
        // The decisive assertion: the user's attempt survived (a broken guard would have run
        // DELETE FROM Attempts and this would be 0).
        Assert.Equal(1, await verify.Attempts.CountAsync());
    }

    public void Dispose() => _factory.Dispose();
}
