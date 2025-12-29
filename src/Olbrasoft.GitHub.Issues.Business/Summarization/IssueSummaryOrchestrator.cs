using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Orchestrates AI summarization for GitHub issues.
/// Single responsibility: AI summarization orchestration only (SRP).
/// Refactored from IssueDetailService to follow Single Responsibility Principle.
/// </summary>
public class IssueSummaryOrchestrator : IIssueSummaryOrchestrator
{
    private readonly IIssueSummaryService _summaryService;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IIssueDetailQueryService _queryService;
    private readonly ILogger<IssueSummaryOrchestrator> _logger;

    public IssueSummaryOrchestrator(
        IIssueSummaryService summaryService,
        IGitHubGraphQLClient graphQLClient,
        IIssueDetailQueryService queryService,
        ILogger<IssueSummaryOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(summaryService);
        ArgumentNullException.ThrowIfNull(graphQLClient);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(logger);

        _summaryService = summaryService;
        _graphQLClient = graphQLClient;
        _queryService = queryService;
        _logger = logger;
    }

    public Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
        => GenerateSummaryAsync(issueId, "both", cancellationToken);

    public async Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GenerateSummary] START for issue {Id}, language={Language}", issueId, language);

        var issues = await _queryService.GetIssuesByIdsAsync(new[] { issueId }, cancellationToken);
        var issue = issues.FirstOrDefault();

        if (issue == null)
        {
            _logger.LogWarning("[GenerateSummary] Issue {Id} not found", issueId);
            return;
        }

        _logger.LogInformation("[GenerateSummary] Issue {Id} found: {Title}", issueId, issue.Title);

        // Fetch body from GraphQL
        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);
        string? body = null;

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            _logger.LogInformation("[GenerateSummary] Fetching body from GraphQL for {Owner}/{Repo}#{Number}", owner, repoName, issue.Number);
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);
            bodies.TryGetValue((owner, repoName, issue.Number), out body);
            _logger.LogInformation("[GenerateSummary] Body fetched, length: {Length}", body?.Length ?? 0);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[GenerateSummary] No body available for issue {Id} - cannot generate summary", issueId);
            return;
        }

        await _summaryService.GenerateSummaryAsync(issueId, body, language, cancellationToken);
    }

    public async Task GenerateSummariesAsync(
        IEnumerable<(int IssueId, string Body)> issuesWithBodies,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var issues = issuesWithBodies.ToList();
        _logger.LogInformation("[GenerateSummaries] Triggering summarization for {Count} issues", issues.Count);

        // Trigger summarization for each issue (sequential to avoid LLM overload)
        foreach (var (issueId, body) in issues)
        {
            try
            {
                await _summaryService.GenerateSummaryAsync(issueId, body, language, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GenerateSummaries] Summarization failed for issue {Id}", issueId);
            }
        }

        _logger.LogInformation("[GenerateSummaries] COMPLETE for {Count} issues", issues.Count);
    }

    public Task GenerateSummaryFromBodyAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        return _summaryService.GenerateSummaryAsync(issueId, body, language, cancellationToken);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
