using System.Security.Claims;
using System.Threading.RateLimiting;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SkillIssue.Application;
using SkillIssue.Application.Services;
using SkillIssue.Data;
using SkillIssue.Data.Seeding;
using SkillIssue.Web.Components;
using SkillIssue.Web.Security;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=skillissue.db";

var githubPat = builder.Configuration["GitHub:PatToken"];

builder.Services
    .AddDataServices(connectionString)
    .AddApplicationServices(githubPat);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Lax;
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

// Rate limiting — login endpoint (per-IP, unauthenticated callers)
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddFixedWindowLimiter("login", cfg =>
    {
        cfg.PermitLimit          = 10;
        cfg.Window               = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit           = 0;
    });
});

// Fork submission rate limiter — per authenticated user, used inside the Blazor circuit.
// The Blazor SignalR circuit bypasses HTTP middleware, so this runs as a singleton service
// rather than via RequireRateLimiting on a route.
builder.Services.AddSingleton<IForkSubmissionLimiter, ForkSubmissionLimiter>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<CircuitOptions>(opts =>
    opts.DetailedErrors = builder.Environment.IsDevelopment());

var app = builder.Build();

// Must be first: populates HttpContext.Connection.RemoteIpAddress and scheme
// from X-Forwarded-For / X-Forwarded-Proto set by the reverse proxy.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

// Security response headers applied to every response including static assets.
// CSP is intentionally omitted: Blazor Server requires 'unsafe-inline' scripts
// for its SignalR bootstrap; a meaningful CSP needs nonce injection (future work).
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "SAMEORIGIN";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();

// Auth endpoints — handled via regular HTTP, not Blazor
app.MapGet("/login", (string? returnUrl) =>
{
    // Reject non-local return URLs to prevent open redirect attacks.
    if (returnUrl is not null && !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        returnUrl = "/";
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        [GitHubAuthenticationDefaults.AuthenticationScheme]);
}).RequireRateLimiting("login");

app.MapPost("/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Apply migrations and seed on startup
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using (var db = await factory.CreateDbContextAsync())
{
    try
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Database startup failed — the application cannot start.");
        throw;
    }
}

app.Run();
