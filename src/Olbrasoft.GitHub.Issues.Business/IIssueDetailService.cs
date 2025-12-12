using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for fetching issue details including body and AI summary.
/// Single responsibility: Orchestrate data retrieval for issue detail page.
/// </summary>
public interface IIssueDetailService
{
    /// <summary>
    /// Gets issue detail by ID, including body from GraphQL.
    /// Returns cached summary if available, otherwise sets SummaryPending = true.
    /// </summary>
    Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI summary for issue and sends notification via SignalR.
    /// Should be called from background task when SummaryPending = true.
    /// </summary>
    Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI summary for issue with language preference and sends notification via SignalR.
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="language">Language preference: "en" (English only), "cs" (Czech only), "both" (English first, then Czech)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches bodies for multiple issues from GitHub GraphQL API and sends previews via SignalR.
    /// </summary>
    /// <param name="issueIds">List of database issue IDs to fetch bodies for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task FetchBodiesAsync(IEnumerable<int> issueIds, CancellationToken cancellationToken = default);
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
    string? ErrorMessage,
    bool SummaryPending = false);

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
