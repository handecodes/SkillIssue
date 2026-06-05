using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public interface IUserService
{
    Task<User> GetOrCreateUserAsync(string githubId, string login, string displayName, string? avatarUrl);
    Task<User?> GetByIdAsync(int userId);
}
