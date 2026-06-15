using SkillIssue.Application.Security;

namespace SkillIssue.Tests;

public class ReturnUrlValidatorTests
{
    [Theory]
    // Open-redirect vectors — all must collapse to "/"
    [InlineData("//evil.com", "/")]
    [InlineData("/\\evil.com", "/")]
    [InlineData("http://evil.com", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("evil.com", "/")]          // no leading slash → not local
    [InlineData("", "/")]
    [InlineData(null, "/")]
    // Legitimate local paths — preserved verbatim
    [InlineData("/progress", "/progress")]
    [InlineData("/challenge/7", "/challenge/7")]
    [InlineData("/", "/")]
    public void Sanitize_ReturnsExpected(string? input, string expected)
    {
        var result = ReturnUrlValidator.Sanitize(input);

        Assert.Equal(expected, result);
    }
}
