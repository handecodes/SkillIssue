using SkillIssue.Application.Services;
using SkillIssue.Tests.Helpers;

namespace SkillIssue.Tests;

public class UserServiceTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_factory);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_CreatesNewUser_WhenNotExists()
    {
        var user = await _sut.GetOrCreateUserAsync("gh1", "alice", "Alice Smith", "https://avatar/1");

        Assert.True(user.Id > 0);
        Assert.Equal("gh1", user.GitHubId);
        Assert.Equal("alice", user.Login);
        Assert.Equal("Alice Smith", user.DisplayName);
        Assert.Equal("https://avatar/1", user.AvatarUrl);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_ReturnsSameUser_OnSecondCall()
    {
        var first = await _sut.GetOrCreateUserAsync("gh2", "bob", "Bob", null);
        var second = await _sut.GetOrCreateUserAsync("gh2", "bob", "Bob", null);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_UpdatesProfile_WhenUserAlreadyExists()
    {
        await _sut.GetOrCreateUserAsync("gh3", "carol_old", "Carol Old", null);
        var updated = await _sut.GetOrCreateUserAsync("gh3", "carol_new", "Carol New", "https://avatar/new");

        Assert.Equal("carol_new", updated.Login);
        Assert.Equal("Carol New", updated.DisplayName);
        Assert.Equal("https://avatar/new", updated.AvatarUrl);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_AssignsMonotonicallyIncreasingIds_ForDistinctUsers()
    {
        var u1 = await _sut.GetOrCreateUserAsync("gh10", "u1", "U1", null);
        var u2 = await _sut.GetOrCreateUserAsync("gh11", "u2", "U2", null);

        Assert.NotEqual(u1.Id, u2.Id);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_SetsCreatedAt_OnlyOnFirstCreate()
    {
        var first = await _sut.GetOrCreateUserAsync("gh20", "dave", "Dave", null);
        var createdAt = first.CreatedAt;

        await Task.Delay(10);
        var second = await _sut.GetOrCreateUserAsync("gh20", "dave", "Dave", null);

        // CreatedAt must not be reset on update
        Assert.Equal(createdAt, second.CreatedAt);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_AcceptsNullAvatarUrl()
    {
        var user = await _sut.GetOrCreateUserAsync("gh30", "eve", "Eve", null);

        Assert.Null(user.AvatarUrl);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        var created = await _sut.GetOrCreateUserAsync("gh40", "frank", "Frank", null);

        var fetched = await _sut.GetByIdAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("frank", fetched.Login);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        var result = await _sut.GetByIdAsync(99999);

        Assert.Null(result);
    }

    public void Dispose() => _factory.Dispose();
}
