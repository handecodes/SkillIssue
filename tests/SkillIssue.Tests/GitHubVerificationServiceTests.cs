using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkillIssue.Application.Services;

namespace SkillIssue.Tests;

public class GitHubVerificationServiceTests
{
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyForkAsync_ReturnsFailure_ForInvalidUrl(string url)
    {
        var sut = BuildService(HttpStatusCode.OK, "{}");

        var result = await sut.VerifyForkAsync(url);

        Assert.False(result.Passed);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenRepoNotFound()
    {
        var sut = BuildService(HttpStatusCode.NotFound, "{}");

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsPassed_WhenLatestRunSucceeded()
    {
        var body = JsonSerializer.Serialize(new
        {
            workflow_runs = new[]
            {
                new { conclusion = "success", head_sha = "abc1234567890", status = "completed" }
            }
        });
        var sut = BuildService(HttpStatusCode.OK, body);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.True(result.Passed);
        Assert.Equal("abc1234567890", result.CommitSha);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailed_WhenLatestRunFailed()
    {
        var body = JsonSerializer.Serialize(new
        {
            workflow_runs = new[]
            {
                new { conclusion = "failure", head_sha = "def456", status = "completed" }
            }
        });
        var sut = BuildService(HttpStatusCode.OK, body);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains("failure", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenNoRunsFound()
    {
        var body = JsonSerializer.Serialize(new { workflow_runs = Array.Empty<object>() });
        var sut = BuildService(HttpStatusCode.OK, body);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains("No completed workflow runs", result.Message);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo.git/")]
    [InlineData("https://github.com/owner/repo/")]
    public async Task VerifyForkAsync_AcceptsValidUrlVariants(string url)
    {
        var body = JsonSerializer.Serialize(new
        {
            workflow_runs = new[]
            {
                new { conclusion = "success", head_sha = "abc123", status = "completed" }
            }
        });
        var sut = BuildService(HttpStatusCode.OK, body);

        var result = await sut.VerifyForkAsync(url);

        Assert.True(result.Passed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task VerifyForkAsync_ReturnsFailure_OnNon200NonNotFoundStatus(HttpStatusCode statusCode)
    {
        var sut = BuildService(statusCode, "{}");

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains(((int)statusCode).ToString(), result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenResponseBodyIsMalformedJson()
    {
        var sut = BuildService(HttpStatusCode.OK, "not json at all {{{");

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnHttpRequestException()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("Network error"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains("Network error", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnTimeout()
    {
        var handler = new ThrowingHttpHandler(new TaskCanceledException("Request timed out"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo");

        Assert.False(result.Passed);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GitHubVerificationService BuildService(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpHandler(statusCode, responseBody);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubVerificationService(client);
    }

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
