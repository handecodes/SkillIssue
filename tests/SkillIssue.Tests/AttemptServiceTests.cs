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

    [Fact]
    public async Task RecordAttemptAsync_AcceptsNullElapsedSeconds()
    {
        var (userId, bugId) = await SeedUserAndBugAsync();

        var attempt = await _sut.RecordAttemptAsync(userId, bugId, "http://fork", passed: false, hintsUsed: 0, elapsedSeconds: null);

        Assert.Null(attempt.ElapsedSeconds);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_IsolatesDataBetweenUsers()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = new Repo { Name = "R", GitHubUrl = "http://r" };
        var bug = new Bug { Title = "B", Repo = repo };
        var user1 = new User { GitHubId = "gh-u1", Login = "u1", DisplayName = "U1", CreatedAt = DateTime.UtcNow };
        var user2 = new User { GitHubId = "gh-u2", Login = "u2", DisplayName = "U2", CreatedAt = DateTime.UtcNow };
        db.Repos.Add(repo);
        db.Bugs.Add(bug);
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        await _sut.RecordAttemptAsync(user1.Id, bug.Id, "http://fork1", passed: true, hintsUsed: 2, elapsedSeconds: null);

        var summary1 = await _sut.GetProgressSummaryAsync(user1.Id);
        var summary2 = await _sut.GetProgressSummaryAsync(user2.Id);

        Assert.Equal(1, summary1.UniqueSolved);
        Assert.Equal(0, summary2.UniqueSolved);
        Assert.Equal(0, summary2.TotalAttempts);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_ReportsTotalBugsPerRepo()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = new Repo { Name = "Multi", GitHubUrl = "http://m" };
        repo.Bugs.Add(new Bug { Title = "Bug1" });
        repo.Bugs.Add(new Bug { Title = "Bug2" });
        var user = new User { GitHubId = "gh-multi", Login = "m", DisplayName = "M", CreatedAt = DateTime.UtcNow };
        db.Repos.Add(repo);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var summary = await _sut.GetProgressSummaryAsync(user.Id);

        Assert.Equal(2, summary.ByRepo[0].TotalBugs);
        Assert.Equal(0, summary.ByRepo[0].Solved);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_CountsMultipleSolvedBugsInSameRepo()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = new Repo { Name = "R2", GitHubUrl = "http://r2" };
        var bug1 = new Bug { Title = "B1", Repo = repo };
        var bug2 = new Bug { Title = "B2", Repo = repo };
        var user = new User { GitHubId = "gh-s2", Login = "s2", DisplayName = "S2", CreatedAt = DateTime.UtcNow };
        db.Bugs.AddRange(bug1, bug2);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await _sut.RecordAttemptAsync(user.Id, bug1.Id, "http://f1", passed: true, hintsUsed: 0, elapsedSeconds: null);
        await _sut.RecordAttemptAsync(user.Id, bug2.Id, "http://f2", passed: true, hintsUsed: 0, elapsedSeconds: null);

        var summary = await _sut.GetProgressSummaryAsync(user.Id);

        Assert.Equal(2, summary.UniqueSolved);
        Assert.Equal(2, summary.ByRepo.Single(r => r.RepoName == "R2").Solved);
    }

    private async Task<(int userId, int bugId)> SeedUserAndBugAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = new Repo { Name = "TestRepo", GitHubUrl = "http://test" };
        var bug = new Bug { Title = "Test Bug", Repo = repo };
        var user = new User { GitHubId = "gh42", Login = "tester", DisplayName = "Tester", CreatedAt = DateTime.UtcNow };
        db.Repos.Add(repo);
        db.Bugs.Add(bug);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, bug.Id);
    }

    public void Dispose() => _factory.Dispose();
}
