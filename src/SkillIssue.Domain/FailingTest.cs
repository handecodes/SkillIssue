namespace SkillIssue.Domain;

public class FailingTest
{
    public int Id { get; set; }
    public int BugId { get; set; }
    public Bug Bug { get; set; } = null!;
    public int Order { get; set; }
    public string TestName { get; set; } = "";
}
