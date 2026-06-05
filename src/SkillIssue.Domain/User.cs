namespace SkillIssue.Domain;

public class User
{
    public int Id { get; set; }
    public string GitHubId { get; set; } = "";
    public string Login { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Attempt> Attempts { get; set; } = [];
}
