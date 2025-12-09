namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class Label
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
}
