namespace SkillIssue.Application.Models;

public record VerificationResult(bool Passed, string? CommitSha, string? Message);
