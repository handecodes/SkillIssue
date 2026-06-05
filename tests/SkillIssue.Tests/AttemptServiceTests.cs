using SkillIssue.Application.Services;
using SkillIssue.Data;
using SkillIssue.Domain;
using SkillIssue.Tests.Helpers;

namespace SkillIssue.Tests;

public class AttemptServiceTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();
    private readonly AttemptService _sut;

    public AttemptServiceTests()
    {
        _sut = new AttemptService(_factory);
    }

    [Fact]
    public async Task RecordAttemptAsync_PersistsAttempt()
    {
        var (userId, bugId) = await SeedUserAndBugAsync();

        var attempt = await _sut.RecordAttemptAsync(userId, bugId, "http://fork", passed: true, hintsUsed: 1, elapsedSeconds: 120);

        Assert.True(attempt.Id > 0);
        Assert.Equal(userId, attempt.UserId);
        Assert.Equal(bugId, attempt.BugId);
        Assert.True(attempt.Passed);
        Assert.Equal(1, attempt.HintsUsed);
        Assert.Equal(120, attempt.ElapsedSeconds);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_CountsUniqueSolvedCorrectly()
    {
        var (userId, bugId) = await SeedUserAndBugAsync();
        // Two attempts on the same bug — should still count as 1 solved
        await _sut.RecordAttemptAsync(userId, bugId, "http://fork1", passed: false, hintsUsed: 0, elapsedSeconds: null);
        await _sut.RecordAttemptAsync(userId, bugId, "http://fork2", passed: true, hintsUsed: 1, elapsedSeconds: 60);

        var summary = await _sut.GetProgressSummaryAsync(userId);

        Assert.Equal(2, summary.TotalAttempts);
        Assert.Equal(1, summary.UniqueSolved);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_AccumulatesHintsUsedPerRepo()
    {
        var (userId, bugId) = await SeedUserAndBugAsync();
        await _sut.RecordAttemptAsync(userId, bugId, "http://fork1", passed: false, hintsUsed: 2, elapsedSeconds: null);
        await _sut.RecordAttemptAsync(userId, bugId, "http://fork2", passed: true, hintsUsed: 1, elapsedSeconds: 30);

        var summary = await _sut.GetProgressSummaryAsync(userId);

        Assert.Equal(3, summary.ByRepo[0].HintsUsed);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_ReturnsEmptySummary_ForNewUser()
    {
        int userId = 0;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var user = new User { GitHubId = "gh99", Login = "newuser", DisplayName = "New" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var summary = await _sut.GetProgressSummaryAsync(userId);

        Assert.Equal(0, summary.TotalAttempts);
        Assert.Equal(0, summary.UniqueSolved);
    }

    private async Task<(int userId, int bugId)> SeedUserAndBugAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = new Repo { Name = "TestRepo", GitHubUrl = "http://test" };
        var bug = new Bug { Title = "Test Bug", Repo = repo, FailingTests = "T1" };
        var user = new User { GitHubId = "gh42", Login = "tester", DisplayName = "Tester", CreatedAt = DateTime.UtcNow };
        db.Repos.Add(repo);
        db.Bugs.Add(bug);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, bug.Id);
    }

    public void Dispose() => _factory.Dispose();
}
