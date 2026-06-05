using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SkillIssue.Application;
using SkillIssue.Application.Services;
using SkillIssue.Data;
using SkillIssue.Data.Seeding;
using SkillIssue.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=skillissue.db";

builder.Services
    .AddDataServices(connectionString)
    .AddApplicationServices();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["GitHub:ClientId"]
            ?? throw new InvalidOperationException("GitHub:ClientId is not configured. Use user-secrets: dotnet user-secrets set \"GitHub:ClientId\" \"<value>\"");
        options.ClientSecret = builder.Configuration["GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GitHub:ClientSecret is not configured. Use user-secrets: dotnet user-secrets set \"GitHub:ClientSecret\" \"<value>\"");
        options.CallbackPath = "/signin-github";
        options.Scope.Add("read:user");
        options.Events.OnCreatingTicket = async ctx =>
        {
            var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();

            // ClaimTypes.NameIdentifier = GitHub numeric ID
            // ClaimTypes.Name           = GitHub login (username)
            // GitHubAuthenticationConstants.Claims.Name = display name from GitHub profile
            var githubId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var login = ctx.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var displayName = ctx.Principal?.FindFirst(GitHubAuthenticationConstants.Claims.Name)?.Value ?? login;
            var avatarUrl = ctx.User.TryGetProperty("avatar_url", out var avatarProp)
                ? avatarProp.GetString()
                : null;

            var user = await userService.GetOrCreateUserAsync(githubId, login, displayName, avatarUrl);

            ctx.Identity!.AddClaim(new Claim("skill_issue:user_id", user.Id.ToString()));
            if (avatarUrl is not null)
                ctx.Identity!.AddClaim(new Claim("urn:github:avatar", avatarUrl));
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Auth endpoints — handled via regular HTTP, not Blazor
app.MapGet("/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        [GitHubAuthenticationDefaults.AuthenticationScheme]));

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Apply migrations and seed on startup
var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using (var db = await factory.CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
