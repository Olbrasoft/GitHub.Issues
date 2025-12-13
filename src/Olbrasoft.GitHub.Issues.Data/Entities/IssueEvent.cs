namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class IssueEvent
{
    public int Id { get; set; }
    public long GitHubEventId { get; set; }
    public int IssueId { get; set; }
    public int EventTypeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Issue Issue { get; set; } = null!;
    public EventType EventType { get; set; } = null!;
}
