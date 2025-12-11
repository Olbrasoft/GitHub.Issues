using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for fetching issue details including body and AI summary.
/// Single responsibility: Orchestrate data retrieval for issue detail page.
/// </summary>
public interface IIssueDetailService
{
    /// <summary>
    /// Gets issue detail by ID, including body from GraphQL and AI summary.
    /// </summary>
    Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of issue detail retrieval.
/// </summary>
public record IssueDetailResult(
    bool Found,
    IssueDetailDto? Issue,
    string? Summary,
    string? SummaryProvider,
    string? SummaryError,
    string? ErrorMessage);

/// <summary>
/// DTO for issue detail page.
/// </summary>
public record IssueDetailDto(
    int Id,
    int IssueNumber,
    string Title,
    bool IsOpen,
    string Url,
    string Owner,
    string RepoName,
    string RepositoryName,
    string? Body,
    List<LabelDto> Labels)
{
    public string StateCzech => IsOpen ? "Otevřený" : "Zavřený";
}
