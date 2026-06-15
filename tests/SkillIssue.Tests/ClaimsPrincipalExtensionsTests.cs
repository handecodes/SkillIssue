using System.Security.Claims;
using SkillIssue.Application.Extensions;

namespace SkillIssue.Tests;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Fact]
    public void GetGitHubId_ReturnsNameIdentifierValue()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "1234567"));

        Assert.Equal("1234567", principal.GetGitHubId());
    }

    [Fact]
    public void GetGitHubId_ReturnsNull_WhenNameIdentifierAbsent()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Name, "alice"));

        Assert.Null(principal.GetGitHubId());
    }

    [Fact]
    public void GetUserId_ParsesUserIdClaim()
    {
        var principal = PrincipalWith(new Claim("skill_issue:user_id", "42"));

        Assert.Equal(42, principal.GetUserId());
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void GetUserId_ReturnsNull_WhenClaimIsGarbage(string value)
    {
        var principal = PrincipalWith(new Claim("skill_issue:user_id", value));

        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenClaimAbsent()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "1234567"));

        Assert.Null(principal.GetUserId());
    }
}
