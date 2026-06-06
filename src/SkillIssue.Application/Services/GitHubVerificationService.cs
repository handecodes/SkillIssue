using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SkillIssue.Application.Models;

namespace SkillIssue.Application.Services;

public partial class GitHubVerificationService(HttpClient httpClient) : IGitHubVerificationService
{
    [GeneratedRegex(@"github\.com/([^/]+)/([^/?\s]+?)(?:\.git)?/?$")]
    private static partial Regex ForkUrlRegex();

    public async Task<VerificationResult> VerifyForkAsync(string forkUrl)
    {
        var match = ForkUrlRegex().Match(forkUrl.Trim());
        if (!match.Success)
            return new VerificationResult(false, null, "Invalid GitHub repository URL. Expected format: https://github.com/owner/repo");

        var owner = match.Groups[1].Value;
        var repo = match.Groups[2].Value;

        try
        {
            var response = await httpClient.GetAsync($"repos/{owner}/{repo}/actions/runs?per_page=10&status=completed");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new VerificationResult(false, null, "Repository not found. Make sure it's public and the URL is correct.");

            if (!response.IsSuccessStatusCode)
                return new VerificationResult(false, null, $"GitHub API returned {(int)response.StatusCode}. Try again shortly.");

            var data = await response.Content.ReadFromJsonAsync<WorkflowRunsResponse>();
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
        catch (System.Text.Json.JsonException)
        {
            return new VerificationResult(false, null, "Unexpected response from GitHub API. Please try again.");
        }
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

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }
}
