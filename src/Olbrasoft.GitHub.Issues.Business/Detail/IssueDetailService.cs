using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Detail;

/// <summary>
/// Service for fetching issue details including body from GraphQL and AI summary.
/// REFACTORED (Issue #278): Now delegates to specialized services following SRP.
/// This class is kept for backward compatibility but internally uses:
/// - IIssueDetailQueryService for database queries
/// - IIssueBodyFetchService for GitHub API interaction
/// - IssueSummaryOrchestrator for AI summarization
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly IIssueDetailQueryService _queryService;
    private readonly IIssueBodyFetchService _bodyFetchService;
    private readonly IIssueSummaryOrchestrator _summaryOrchestrator;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ILogger<IssueDetailService> _logger;

    public IssueDetailService(
        IIssueDetailQueryService queryService,
        IIssueBodyFetchService bodyFetchService,
        IIssueSummaryOrchestrator summaryOrchestrator,
        IGitHubGraphQLClient graphQLClient,
        ILogger<IssueDetailService> logger)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(bodyFetchService);
        ArgumentNullException.ThrowIfNull(summaryOrchestrator);
        ArgumentNullException.ThrowIfNull(graphQLClient);
        ArgumentNullException.ThrowIfNull(logger);

        _queryService = queryService;
        _bodyFetchService = bodyFetchService;
        _summaryOrchestrator = summaryOrchestrator;
        _graphQLClient = graphQLClient;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IssueDetailService] Delegating to IssueDetailQueryService");

        // Delegate to specialized query service
        var result = await _queryService.GetIssueDetailAsync(issueId, cancellationToken);

        if (!result.Found)
        {
            return result;
        }

        // Fetch body from GitHub GraphQL API
        var owner = result.Issue!.Owner;
        var repoName = result.Issue.RepoName;
        string? body = null;

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            var requests = new[] { new IssueBodyRequest(owner, repoName, result.Issue.IssueNumber) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

            var key = (owner, repoName, result.Issue.IssueNumber);
            if (bodies.TryGetValue(key, out body))
            {
                result = result with
                {
                    Issue = result.Issue with { Body = body }
                };
            }
        }

        // Summary always generated on-demand via SignalR (no caching)
        var summaryPending = !string.IsNullOrWhiteSpace(body);
        if (summaryPending)
        {
            _logger.LogDebug("Summary pending for issue {Id} - will be generated via SignalR", issueId);
        }

        return result with { SummaryPending = summaryPending };
    }

    /// <summary>
    /// Generates AI summary for issue and sends notification via SignalR.
    /// Called from background task when SummaryPending = true.
    /// Default behavior: generates both English and Czech summaries.
    /// </summary>
    public Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IssueDetailService] Delegating to IssueSummaryOrchestrator");
        return _summaryOrchestrator.GenerateSummaryAsync(issueId, cancellationToken);
    }

    /// <summary>
    /// Generates AI summary for issue with language preference and sends notification via SignalR.
    /// </summary>
    public Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IssueDetailService] Delegating to IssueSummaryOrchestrator with language={Language}", language);
        return _summaryOrchestrator.GenerateSummaryAsync(issueId, language, cancellationToken);
    }

    /// <summary>
    /// Generates AI summary from a pre-fetched body and sends notification via SignalR.
    /// </summary>
    public Task GenerateSummaryFromBodyAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IssueDetailService] Delegating to IssueSummaryOrchestrator for pre-fetched body");
        return _summaryOrchestrator.GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
    }

    /// <summary>
    /// Fetches bodies for multiple issues from GitHub GraphQL API and sends previews via SignalR.
    /// Also triggers AI summarization for each issue with a body.
    /// </summary>
    public async Task FetchBodiesAsync(IEnumerable<int> issueIds, string language = "en", CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IssueDetailService] Delegating to IssueBodyFetchService and IssueSummaryOrchestrator");

        var idList = issueIds.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        // Delegate body fetching to specialized service
        await _bodyFetchService.FetchBodiesAsync(idList, cancellationToken);

        // Note: IssueBodyFetchService already sends body previews via SignalR
        // Now trigger summarization for those issues
        // However, IssueBodyFetchService doesn't return the bodies,
        // so we need to fetch them again for summarization.
        // Let's fetch bodies here for summarization purposes
        var issues = await _queryService.GetIssuesByIdsAsync(idList, cancellationToken);

        if (issues.Count == 0)
        {
            return;
        }

        // Build GraphQL requests
        var requests = issues
            .Select(i =>
            {
                var (owner, repo) = ParseRepositoryFullName(i.Repository.FullName);
                return new IssueBodyRequest(owner, repo, i.Number);
            })
            .Where(r => !string.IsNullOrEmpty(r.Owner) && !string.IsNullOrEmpty(r.Repo))
            .ToList();

        if (requests.Count == 0)
        {
            return;
        }

        // Batch fetch from GraphQL
        var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

        // Collect issues with bodies for summarization
        var issuesWithBodies = new List<(int IssueId, string Body)>();

        foreach (var issue in issues)
        {
            var (owner, repo) = ParseRepositoryFullName(issue.Repository.FullName);
            var key = (owner, repo, issue.Number);

            if (bodies.TryGetValue(key, out var body) && !string.IsNullOrWhiteSpace(body))
            {
                issuesWithBodies.Add((issue.Id, body));
            }
        }

        // Delegate summarization to orchestrator
        await _summaryOrchestrator.GenerateSummariesAsync(issuesWithBodies, language, cancellationToken);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
