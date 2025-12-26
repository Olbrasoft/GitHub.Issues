using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue details including body from GraphQL and AI summary.
/// Refactored to use separate services for preview generation and summarization.
/// Updated to follow DIP by depending on IIssueRepository abstraction.
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly IIssueRepository _issueRepository;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IIssueSummaryService _summaryService;
    private readonly IBodyPreviewGenerator _previewGenerator;
    private readonly IBodyNotifier _bodyNotifier;
    private readonly BodyPreviewSettings _bodyPreviewSettings;
    private readonly ILogger<IssueDetailService> _logger;

    public IssueDetailService(
        IIssueRepository issueRepository,
        IGitHubGraphQLClient graphQLClient,
        IIssueSummaryService summaryService,
        IBodyPreviewGenerator previewGenerator,
        IBodyNotifier bodyNotifier,
        IOptions<BodyPreviewSettings> bodyPreviewSettings,
        ILogger<IssueDetailService> logger)
    {
        ArgumentNullException.ThrowIfNull(issueRepository);
        ArgumentNullException.ThrowIfNull(graphQLClient);
        ArgumentNullException.ThrowIfNull(summaryService);
        ArgumentNullException.ThrowIfNull(previewGenerator);
        ArgumentNullException.ThrowIfNull(bodyNotifier);
        ArgumentNullException.ThrowIfNull(bodyPreviewSettings);
        ArgumentNullException.ThrowIfNull(logger);

        _issueRepository = issueRepository;
        _graphQLClient = graphQLClient;
        _summaryService = summaryService;
        _previewGenerator = previewGenerator;
        _bodyNotifier = bodyNotifier;
        _bodyPreviewSettings = bodyPreviewSettings.Value;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issueRepository.GetIssueWithDetailsAsync(issueId, cancellationToken);

        if (issue == null)
        {
            return new IssueDetailResult(
                Found: false,
                Issue: null,
                Summary: null,
                SummaryProvider: null,
                SummaryError: null,
                ErrorMessage: "Issue nenalezeno.");
        }

        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);

        var labels = issue.IssueLabels
            .Select(il => new LabelDto(il.Label.Name, il.Label.Color))
            .ToList();

        var issueDto = new IssueDetailDto(
            Id: issue.Id,
            IssueNumber: issue.Number,
            Title: issue.Title,
            IsOpen: issue.IsOpen,
            Url: issue.Url,
            Owner: owner,
            RepoName: repoName,
            RepositoryName: issue.Repository.FullName,
            Body: null,
            Labels: labels);

        // Fetch body from GitHub GraphQL API
        string? body = null;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

            var key = (owner, repoName, issue.Number);
            if (bodies.TryGetValue(key, out body))
            {
                issueDto = issueDto with { Body = body };
            }
        }

        // Summary always generated on-demand via SignalR (no caching)
        var summaryPending = !string.IsNullOrWhiteSpace(body);
        if (summaryPending)
        {
            _logger.LogDebug("Summary pending for issue {Id} - will be generated via SignalR", issueId);
        }

        return new IssueDetailResult(
            Found: true,
            Issue: issueDto,
            Summary: null,
            SummaryProvider: null,
            SummaryError: null,
            ErrorMessage: null,
            SummaryPending: summaryPending);
    }

    /// <summary>
    /// Generates AI summary for issue and sends notification via SignalR.
    /// Called from background task when SummaryPending = true.
    /// Default behavior: generates both English and Czech summaries.
    /// </summary>
    public Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
        => GenerateSummaryAsync(issueId, "both", cancellationToken);

    /// <summary>
    /// Generates AI summary for issue with language preference and sends notification via SignalR.
    /// </summary>
    public async Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GenerateSummary] START for issue {Id}, language={Language}", issueId, language);

        var issue = await _issueRepository.GetIssueWithRepositoryAsync(issueId, cancellationToken);

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

    /// <summary>
    /// Generates AI summary from a pre-fetched body and sends notification via SignalR.
    /// </summary>
    public Task GenerateSummaryFromBodyAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        return _summaryService.GenerateSummaryAsync(issueId, body, language, cancellationToken);
    }

    /// <summary>
    /// Fetches bodies for multiple issues from GitHub GraphQL API and sends previews via SignalR.
    /// Also triggers AI summarization for each issue with a body.
    /// </summary>
    public async Task FetchBodiesAsync(IEnumerable<int> issueIds, string language = "en", CancellationToken cancellationToken = default)
    {
        var idList = issueIds.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("[FetchBodies] START for {Count} issues: {Ids}", idList.Count, string.Join(", ", idList));

        // Load issues with repository info
        var issues = await _issueRepository.GetIssuesByIdsWithRepositoryAsync(idList, cancellationToken);

        if (issues.Count == 0)
        {
            _logger.LogWarning("[FetchBodies] No issues found for IDs: {Ids}", string.Join(", ", idList));
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
            _logger.LogWarning("[FetchBodies] No valid requests after parsing repository names");
            return;
        }

        _logger.LogInformation("[FetchBodies] Fetching {Count} bodies from GitHub GraphQL", requests.Count);

        // Batch fetch from GraphQL
        var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

        _logger.LogInformation("[FetchBodies] Received {Count} bodies from GraphQL", bodies.Count);

        // Collect issues with bodies for summarization
        var issuesWithBodies = new List<(int IssueId, string Body)>();

        // Send notifications for each body
        foreach (var issue in issues)
        {
            var (owner, repo) = ParseRepositoryFullName(issue.Repository.FullName);
            var key = (owner, repo, issue.Number);

            if (bodies.TryGetValue(key, out var body) && !string.IsNullOrWhiteSpace(body))
            {
                var preview = _previewGenerator.CreatePreview(body, _bodyPreviewSettings.MaxLength);
                await _bodyNotifier.NotifyBodyReceivedAsync(
                    new BodyNotificationDto(issue.Id, preview),
                    cancellationToken);

                _logger.LogDebug("[FetchBodies] Sent body preview for issue {Id}", issue.Id);

                // Queue for summarization
                issuesWithBodies.Add((issue.Id, body));
            }
            else
            {
                _logger.LogDebug("[FetchBodies] No body found for issue {Id}", issue.Id);
            }
        }

        _logger.LogInformation("[FetchBodies] Body previews sent. Triggering summarization for {Count} issues", issuesWithBodies.Count);

        // Trigger summarization for each issue (sequential to avoid LLM overload)
        foreach (var (issueId, body) in issuesWithBodies)
        {
            try
            {
                await _summaryService.GenerateSummaryAsync(issueId, body, language, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FetchBodies] Summarization failed for issue {Id}", issueId);
            }
        }

        _logger.LogInformation("[FetchBodies] COMPLETE for {Count} issues", idList.Count);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
