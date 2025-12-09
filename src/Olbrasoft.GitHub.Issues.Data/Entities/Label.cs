namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class Label
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "ededed";

    public Repository Repository { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
}
