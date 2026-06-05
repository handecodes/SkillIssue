namespace SkillIssue.Domain;

public class HintTier
{
    public int Id { get; set; }
    public int BugId { get; set; }
    public Bug Bug { get; set; } = null!;
    public int Order { get; set; }
    public string Label { get; set; } = "";
    public string Content { get; set; } = "";
}
