namespace SkillIssue.Domain;

public class Attempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int BugId { get; set; }
    public Bug Bug { get; set; } = null!;
    public string ForkUrl { get; set; } = "";
    public bool Passed { get; set; }
    public int HintsUsed { get; set; }
    public int? ElapsedSeconds { get; set; }
    public DateTime SubmittedAt { get; set; }
}
