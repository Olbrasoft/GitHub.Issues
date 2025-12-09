namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class IssueLabel
{
    public int IssueId { get; set; }
    public int LabelId { get; set; }

    public Issue Issue { get; set; } = null!;
    public Label Label { get; set; } = null!;
}
