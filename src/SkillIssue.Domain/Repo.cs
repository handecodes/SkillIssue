namespace SkillIssue.Domain;

public class Repo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string GitHubUrl { get; set; } = "";
    public string Language { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Bug> Bugs { get; set; } = [];
}
