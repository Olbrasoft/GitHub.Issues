namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Lightweight DTO for sub-issue display in parent issue detail.
/// </summary>
public record SubIssueSummaryDto(int Id, int IssueNumber, string Title, bool IsOpen);
