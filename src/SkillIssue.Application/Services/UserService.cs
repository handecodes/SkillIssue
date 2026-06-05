using Microsoft.EntityFrameworkCore;
using SkillIssue.Data;
using SkillIssue.Domain;

namespace SkillIssue.Application.Services;

public class UserService(IDbContextFactory<AppDbContext> factory) : IUserService
{
    public async Task<User> GetOrCreateUserAsync(string githubId, string login, string displayName, string? avatarUrl)
    {
        await using var db = await factory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);
        if (user is not null)
        {
            user.Login = login;
            user.DisplayName = displayName;
            user.AvatarUrl = avatarUrl;
            await db.SaveChangesAsync();
            return user;
        }

        user = new User
        {
            GitHubId = githubId,
            Login = login,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }
}
