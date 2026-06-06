using System.Net;
using System.Text.Json;
using SkillIssue.Application.Services;

namespace SkillIssue.Tests;

public class GitHubVerificationServiceTests
{
    private const string UpstreamUrl = "https://github.com/expected/repo";

    // Fork-info JSON helpers
    private static string ValidForkInfoJson(string parentUrl = UpstreamUrl) =>
        JsonSerializer.Serialize(new
        {
            fork = true,
            parent = new { html_url = parentUrl, full_name = parentUrl.Split('/')[^1] }
        });

    private static string NotAForkInfoJson() =>
        JsonSerializer.Serialize(new { fork = false });

    // -- Invalid URL (no HTTP call made) -------------------------------------

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyForkAsync_ReturnsFailure_ForInvalidUrl(string url)
    {
        var sut = BuildService((HttpStatusCode.OK, "{}"));

        var result = await sut.VerifyForkAsync(url, UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("Invalid", result.Message);
    }

    // -- Repo-info call failures (first request) -----------------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenRepoNotFound()
    {
        var sut = BuildService((HttpStatusCode.NotFound, "{}"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task VerifyForkAsync_ReturnsFailure_OnNon200NonNotFoundStatus(HttpStatusCode statusCode)
    {
        var sut = BuildService((statusCode, "{}"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains(((int)statusCode).ToString(), result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenResponseBodyIsMalformedJson()
    {
        var sut = BuildService((HttpStatusCode.OK, "not json at all {{{"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
    }

    // -- Fork validation (second request: fork status) -----------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenNotAFork()
    {
        var sut = BuildService((HttpStatusCode.OK, NotAForkInfoJson()));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("not a fork", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenForkParentIsWrongRepo()
    {
        var sut = BuildService((HttpStatusCode.OK, ValidForkInfoJson("https://github.com/different/repo")));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("expected challenge repo", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://github.com/expected/repo")]
    [InlineData("https://github.com/expected/repo/")]
    [InlineData("https://github.com/expected/repo.git")]
    [InlineData("HTTPS://GITHUB.COM/EXPECTED/REPO")]
    public async Task VerifyForkAsync_MatchesUpstreamUrl_CaseAndTrailingVariants(string upstreamVariant)
    {
        // Fork's parent URL is always the canonical form
        var body = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc123" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson("https://github.com/expected/repo")),
            (HttpStatusCode.OK, body));

        var result = await sut.VerifyForkAsync("https://github.com/owner/fork", upstreamVariant);

        Assert.True(result.Passed);
    }

    // -- CI run results (third request: workflow runs) -----------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsPassed_WhenLatestRunSucceeded()
    {
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc1234567890" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.True(result.Passed);
        Assert.Equal("abc1234567890", result.CommitSha);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailed_WhenLatestRunFailed()
    {
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "failure", head_sha = "def456" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("failure", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenNoRunsFound()
    {
        var runsBody = JsonSerializer.Serialize(new { workflow_runs = Array.Empty<object>() });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("No completed workflow runs", result.Message);
    }

    // -- URL format variants -------------------------------------------------

    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo.git/")]
    [InlineData("https://github.com/owner/repo/")]
    public async Task VerifyForkAsync_AcceptsValidForkUrlVariants(string url)
    {
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc123" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync(url, UpstreamUrl);

        Assert.True(result.Passed);
    }

    // -- Transport-level errors ----------------------------------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnHttpRequestException()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("Network error"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("Network error", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnTimeout()
    {
        var handler = new ThrowingHttpHandler(new TaskCanceledException("Request timed out"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl);

        Assert.False(result.Passed);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -- Helpers -------------------------------------------------------------

    private static GitHubVerificationService BuildService(
        (HttpStatusCode status, string body) first,
        (HttpStatusCode status, string body)? second = null)
    {
        var handler = second is null
            ? new SequencedHttpHandler(first)
            : new SequencedHttpHandler(first, second.Value);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubVerificationService(client);
    }

    private sealed class SequencedHttpHandler(params (HttpStatusCode status, string body)[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHttpHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
