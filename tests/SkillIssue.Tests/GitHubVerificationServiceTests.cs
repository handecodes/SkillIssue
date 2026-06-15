using System.Net;
using System.Text.Json;
using SkillIssue.Application.Services;

namespace SkillIssue.Tests;

public class GitHubVerificationServiceTests
{
    private const string UpstreamUrl = "https://github.com/expected/repo";
    // GitHub numeric owner ID the "signed-in user" owns; ValidForkInfoJson reports the same by default.
    private const string OwnerGitHubId = "424242";

    // Fork-info JSON helpers
    private static string ValidForkInfoJson(string parentUrl = UpstreamUrl, string defaultBranch = "main", long ownerId = 424242) =>
        JsonSerializer.Serialize(new
        {
            fork = true,
            default_branch = defaultBranch,
            owner = new { id = ownerId },
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

        var result = await sut.VerifyForkAsync(url, UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("Invalid", result.Message);
    }

    // -- Repo-info call failures (first request) -----------------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenRepoNotFound()
    {
        var sut = BuildService((HttpStatusCode.NotFound, "{}"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

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

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains(((int)statusCode).ToString(), result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenResponseBodyIsMalformedJson()
    {
        var sut = BuildService((HttpStatusCode.OK, "not json at all {{{"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
    }

    // -- Fork validation (second request: fork status) -----------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenNotAFork()
    {
        var sut = BuildService((HttpStatusCode.OK, NotAForkInfoJson()));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("not a fork", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenForkParentIsWrongRepo()
    {
        var sut = BuildService((HttpStatusCode.OK, ValidForkInfoJson("https://github.com/different/repo")));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

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

        var result = await sut.VerifyForkAsync("https://github.com/owner/fork", upstreamVariant, OwnerGitHubId);

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

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

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

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("failure", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyForkAsync_ScopesRunsQuery_ToChallengeWorkflowOnDefaultBranch()
    {
        // Regression guard: Step 2 must query OUR challenge workflow on the fork's default
        // branch, NOT the repo-wide actions/runs endpoint. A fork inherits the upstream's own
        // workflows; a repo-wide query can pick an unrelated workflow's conclusion. The previous
        // tests passed by call-order and never inspected the URL, which is how this slipped in.
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc123" } }
        });
        var handler = new CapturingHttpHandler(
            (HttpStatusCode.OK, ValidForkInfoJson(defaultBranch: "develop")),
            (HttpStatusCode.OK, runsBody));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.True(result.Passed);
        var step2Url = handler.Requests[1].RequestUri!.ToString();
        Assert.Contains("actions/workflows/challenge.yml/runs", step2Url);
        Assert.Contains("branch=develop", step2Url);
        Assert.DoesNotContain("/actions/runs?", step2Url);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_WhenNoRunsFound()
    {
        var runsBody = JsonSerializer.Serialize(new { workflow_runs = Array.Empty<object>() });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("No completed challenge workflow runs", result.Message);
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

        var result = await sut.VerifyForkAsync(url, UpstreamUrl, OwnerGitHubId);

        Assert.True(result.Passed);
    }

    // -- Transport-level errors ----------------------------------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnHttpRequestException()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("Network error"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("Network error", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsFailure_OnTimeout()
    {
        var handler = new ThrowingHttpHandler(new TaskCanceledException("Request timed out"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubVerificationService(client);

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -- Rate limiting (403/429 + X-RateLimit-Remaining: 0) ------------------

    [Fact]
    public async Task VerifyForkAsync_ReturnsRateLimitMessage_When403WithRemainingZero()
    {
        // 403 with X-RateLimit-Remaining: 0 is GitHub's rate-limit signal — the user must be told
        // it's server-side, not a problem with their fix.
        var sut = BuildServiceWithResponse(HttpStatusCode.Forbidden, "{}",
            ("X-RateLimit-Remaining", "0"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("rate-limited on our side", result.Message);
        Assert.Contains("isn't a problem with your fix", result.Message);
        Assert.DoesNotContain("GitHub API returned 403", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsGenericMessage_When403WithoutRateLimitHeader()
    {
        // The discrimination test: a real 403 (no rate-limit header) must NOT be mislabeled as
        // rate limiting — it falls through to the generic message.
        var sut = BuildServiceWithResponse(HttpStatusCode.Forbidden, "{}");

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("GitHub API returned 403", result.Message);
        Assert.DoesNotContain("rate-limited", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_RateLimitMessageIncludesWait_WhenResetHeaderPresent()
    {
        var resetUnix = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();
        var sut = BuildServiceWithResponse(HttpStatusCode.Forbidden, "{}",
            ("X-RateLimit-Remaining", "0"),
            ("X-RateLimit-Reset", resetUnix));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("rate-limited on our side", result.Message);
        Assert.Contains("minute(s)", result.Message);
    }

    // -- Fork ownership (must belong to the signed-in user) ------------------

    [Fact]
    public async Task VerifyForkAsync_Proceeds_WhenForkOwnerMatchesUser()
    {
        // Owner id reported by GitHub matches the caller's id → ownership gate passes and a green
        // run is a PASS.
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc123" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson(ownerId: 424242)),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, "424242");

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task VerifyForkAsync_Rejects_WhenForkOwnedByDifferentUser()
    {
        // Valid fork of the right parent with a GREEN run, but owned by someone else. Must be
        // rejected at the ownership gate — the green run is never trusted. (Owner is compared by
        // GitHub numeric id from the API, not the URL slug.)
        var runsBody = JsonSerializer.Serialize(new
        {
            workflow_runs = new[] { new { conclusion = "success", head_sha = "abc123" } }
        });
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson(ownerId: 999999)),
            (HttpStatusCode.OK, runsBody));

        var result = await sut.VerifyForkAsync("https://github.com/someoneelse/repo", UpstreamUrl, "424242");

        Assert.False(result.Passed);
        Assert.Contains("isn't owned by your GitHub account", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_DoesNotThrow_WhenForkUrlIsNull()
    {
        // Null-guard: a null fork URL must return the invalid-URL message, not throw an NRE.
        var sut = BuildService((HttpStatusCode.OK, "{}"));

        var result = await sut.VerifyForkAsync(null!, UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task VerifyForkAsync_ReturnsWorkflowMissingMessage_WhenRunsEndpoint404()
    {
        // 404 on the workflow-runs endpoint = challenge.yml deleted/renamed — distinct from a
        // transient API error, so the message must not say "try again shortly".
        var sut = BuildService(
            (HttpStatusCode.OK, ValidForkInfoJson()),
            (HttpStatusCode.NotFound, "{}"));

        var result = await sut.VerifyForkAsync("https://github.com/owner/repo", UpstreamUrl, OwnerGitHubId);

        Assert.False(result.Passed);
        Assert.Contains("challenge.yml", result.Message);
        Assert.Contains("wasn't found", result.Message);
        Assert.DoesNotContain("Try again shortly", result.Message);
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

    // Builds a service whose first (and every) GitHub response carries the given status, body,
    // and response headers — needed to simulate rate-limit headers, which the other handlers omit.
    private static GitHubVerificationService BuildServiceWithResponse(
        HttpStatusCode status, string body, params (string name, string value)[] headers)
    {
        var handler = new HeaderedHttpHandler(status, body, headers);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubVerificationService(client);
    }

    private sealed class HeaderedHttpHandler(
        HttpStatusCode status, string body, params (string name, string value)[] headers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            foreach (var (name, value) in headers)
                response.Headers.TryAddWithoutValidation(name, value);
            return Task.FromResult(response);
        }
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

    private sealed class CapturingHttpHandler(params (HttpStatusCode status, string body)[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
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
