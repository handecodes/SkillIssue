using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SkillIssue.Application.Models;

namespace SkillIssue.Application.Services;

public partial class GitHubVerificationService(HttpClient httpClient) : IGitHubVerificationService
{
    [GeneratedRegex(@"github\.com/([^/]+)/([^/?\s]+?)(?:\.git)?/?$")]
    private static partial Regex ForkUrlRegex();

    public async Task<VerificationResult> VerifyForkAsync(string forkUrl, string upstreamUrl)
    {
        var match = ForkUrlRegex().Match(forkUrl.Trim());
        if (!match.Success)
            return new VerificationResult(false, null, "Invalid GitHub repository URL. Expected format: https://github.com/owner/repo");

        var owner = match.Groups[1].Value;
        var repo  = match.Groups[2].Value;

        try
        {
            // Step 1: verify the repo exists and is a fork of the expected upstream.
            var repoResponse = await httpClient.GetAsync($"repos/{owner}/{repo}");

            if (repoResponse.StatusCode == HttpStatusCode.NotFound)
                return new VerificationResult(false, null, "Repository not found. Make sure it's public and the URL is correct.");

            if (!repoResponse.IsSuccessStatusCode)
                return new VerificationResult(false, null, $"GitHub API returned {(int)repoResponse.StatusCode}. Try again shortly.");

            var repoData = await repoResponse.Content.ReadFromJsonAsync<RepoInfoResponse>();

            if (repoData is null || !repoData.Fork)
                return new VerificationResult(false, null, "This repository is not a fork. Fork the challenge repo on GitHub first, then push your fix there.");

            var expectedNorm = NormalizeGitHubUrl(upstreamUrl);
            var parentNorm   = NormalizeGitHubUrl(repoData.Parent?.HtmlUrl ?? "");
            if (!string.Equals(expectedNorm, parentNorm, StringComparison.OrdinalIgnoreCase))
                return new VerificationResult(false, null,
                    $"This fork's parent is '{repoData.Parent?.FullName}', not the expected challenge repo. Make sure you forked the right repository.");

            // Step 2: check latest completed CI run on the fork.
            var runsResponse = await httpClient.GetAsync($"repos/{owner}/{repo}/actions/runs?per_page=10&status=completed");

            if (!runsResponse.IsSuccessStatusCode)
                return new VerificationResult(false, null, $"GitHub API returned {(int)runsResponse.StatusCode}. Try again shortly.");

            var data = await runsResponse.Content.ReadFromJsonAsync<WorkflowRunsResponse>();
            var latestRun = data?.WorkflowRuns.FirstOrDefault();

            if (latestRun is null)
                return new VerificationResult(false, null, "No completed workflow runs found. Make sure your fork has a CI workflow configured.");

            bool passed = latestRun.Conclusion == "success";
            string message = passed
                ? "All CI checks passed — great fix!"
                : $"CI did not pass. Conclusion: {latestRun.Conclusion ?? "unknown"}. Check the Actions tab in your fork.";

            return new VerificationResult(passed, latestRun.HeadSha, message);
        }
        catch (HttpRequestException ex)
        {
            return new VerificationResult(false, null, $"Could not reach GitHub API: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return new VerificationResult(false, null, "GitHub API request timed out. Please try again.");
        }
        catch (JsonException)
        {
            return new VerificationResult(false, null, "Unexpected response from GitHub API. Please try again.");
        }
    }

    private static string NormalizeGitHubUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];
        return url.ToLowerInvariant();
    }

    private sealed class RepoInfoResponse
    {
        [JsonPropertyName("fork")]
        public bool Fork { get; set; }

        [JsonPropertyName("parent")]
        public ParentInfo? Parent { get; set; }
    }

    private sealed class ParentInfo
    {
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = "";
    }

    private sealed class WorkflowRunsResponse
    {
        [JsonPropertyName("workflow_runs")]
        public List<WorkflowRun> WorkflowRuns { get; set; } = [];
    }

    private sealed class WorkflowRun
    {
        [JsonPropertyName("conclusion")]
        public string? Conclusion { get; set; }

        [JsonPropertyName("head_sha")]
        public string HeadSha { get; set; } = "";
    }
}
