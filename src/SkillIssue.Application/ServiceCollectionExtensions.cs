using Microsoft.Extensions.DependencyInjection;
using SkillIssue.Application.Services;

namespace SkillIssue.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<IAttemptService, AttemptService>();

        services.AddHttpClient<IGitHubVerificationService, GitHubVerificationService>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "SkillIssue-App/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
