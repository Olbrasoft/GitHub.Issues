namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class EventType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<IssueEvent> IssueEvents { get; set; } = new List<IssueEvent>();
}
