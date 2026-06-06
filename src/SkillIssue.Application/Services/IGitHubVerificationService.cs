using SkillIssue.Application.Models;

namespace SkillIssue.Application.Services;

public interface IGitHubVerificationService
{
    Task<VerificationResult> VerifyForkAsync(string forkUrl, string upstreamUrl);
}
