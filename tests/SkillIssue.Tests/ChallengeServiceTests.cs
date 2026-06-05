using SkillIssue.Application.Services;
using SkillIssue.Data;
using SkillIssue.Domain;
using SkillIssue.Tests.Helpers;

namespace SkillIssue.Tests;

public class ChallengeServiceTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();
    private readonly ChallengeService _sut;

    public ChallengeServiceTests()
    {
        _sut = new ChallengeService(_factory);
    }

    [Fact]
    public async Task GetActiveReposWithBugsAsync_ReturnsOnlyActiveRepos()
    {
        await SeedAsync(db =>
        {
            db.Repos.Add(new Repo { Name = "Active", GitHubUrl = "http://x", IsActive = true });
            db.Repos.Add(new Repo { Name = "Inactive", GitHubUrl = "http://y", IsActive = false });
        });

        var result = await _sut.GetActiveReposWithBugsAsync();

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetActiveReposWithBugsAsync_IncludesBugs()
    {
        await SeedAsync(db =>
        {
            var repo = new Repo { Name = "R", GitHubUrl = "http://r", IsActive = true };
            repo.Bugs.Add(new Bug { Title = "Bug A", FailingTests = "T1" });
            db.Repos.Add(repo);
        });

        var result = await _sut.GetActiveReposWithBugsAsync();

        Assert.Single(result[0].Bugs);
        Assert.Equal("Bug A", result[0].Bugs[0].Title);
    }

    [Fact]
    public async Task GetBugWithHintsAsync_ReturnsBugWithOrderedHints()
    {
        int bugId = 0;
        await SeedAsync(db =>
        {
            var repo = new Repo { Name = "R", GitHubUrl = "http://r" };
            var bug = new Bug { Title = "B", Repo = repo };
            bug.Hints.Add(new HintTier { Order = 2, Label = "Area", Content = "second" });
            bug.Hints.Add(new HintTier { Order = 1, Label = "Nudge", Content = "first" });
            db.Bugs.Add(bug);
            db.SaveChanges();
            bugId = bug.Id;
        });

        var result = await _sut.GetBugWithHintsAsync(bugId);

        Assert.NotNull(result);
        Assert.Equal(2, result.Hints.Count);
        Assert.Equal(1, result.Hints[0].Order);
        Assert.Equal(2, result.Hints[1].Order);
    }

    [Fact]
    public async Task GetBugWithHintsAsync_ReturnsNull_WhenBugDoesNotExist()
    {
        var result = await _sut.GetBugWithHintsAsync(999);
        Assert.Null(result);
    }

    private async Task SeedAsync(Action<AppDbContext> seed)
    {
        await using var db = await _factory.CreateDbContextAsync();
        seed(db);
        await db.SaveChangesAsync();
    }

    public void Dispose() => _factory.Dispose();
}
