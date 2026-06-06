namespace SkillIssue.Domain;

public class Bug
{
    public int Id { get; set; }
    public int RepoId { get; set; }
    public Repo Repo { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Brief { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public Difficulty Difficulty { get; set; }
    public List<FailingTest> FailingTests { get; set; } = [];
    public List<HintTier> Hints { get; set; } = [];
    public List<Attempt> Attempts { get; set; } = [];
}
