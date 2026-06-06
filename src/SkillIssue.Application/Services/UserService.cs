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
        try
        {
            await db.SaveChangesAsync();
            return user;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Concurrent login: another request inserted the same GitHub user first.
            // Open a fresh context (the faulted one can't be reused) and re-read.
            await using var retryDb = await factory.CreateDbContextAsync();
            var existing = await retryDb.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);
            if (existing is null) throw;
            existing.Login = login;
            existing.DisplayName = displayName;
            existing.AvatarUrl = avatarUrl;
            await retryDb.SaveChangesAsync();
            return existing;
        }
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }
}
