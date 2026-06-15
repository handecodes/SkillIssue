namespace SkillIssue.Application.Security;

public static class ReturnUrlValidator
{
    /// <summary>
    /// Returns a safe, local post-login redirect path. Falls back to "/" for anything that
    /// isn't a single-slash-rooted local path — i.e. null/empty, absolute URLs
    /// ("http://evil.com"), and protocol-relative URLs ("//evil.com", "/\evil.com"), all of
    /// which are open-redirect vectors when used as an OAuth RedirectUri.
    /// </summary>
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
