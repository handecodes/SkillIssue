using System.Reflection;

namespace SkillIssue.Web;

public static class AppVersion
{
    public static readonly string Current =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]   // strip git hash suffix if present
        ?? "unknown";
}
