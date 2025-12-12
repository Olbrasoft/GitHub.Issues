using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue details including body from GraphQL and Czech AI summary.
/// Uses two-step process: English summarization → Czech translation.
/// Summary generation is progressive - page loads immediately, summary arrives via SignalR.
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ISummarizationService _summarizationService;
    private readonly ITranslationService _translationService;
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly ILogger<IssueDetailService> _logger;

    public IssueDetailService(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        ISummarizationService summarizationService,
        ITranslationService translationService,
        ISummaryNotifier summaryNotifier,
        ILogger<IssueDetailService> logger)
    {
        _dbContext = dbContext;
        _graphQLClient = graphQLClient;
        _summarizationService = summarizationService;
        _translationService = translationService;
        _summaryNotifier = summaryNotifier;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .Include(i => i.IssueLabels)
                .ThenInclude(il => il.Label)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

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

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
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
    /// <param name="issueId">Database issue ID</param>
    /// <param name="language">Language preference: "en" (English only), "cs" (Czech only), "both" (English first, then Czech)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GenerateSummary] START for issue {Id}, language={Language}", issueId, language);

        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

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

        // Step 1: Summarize in English
        _logger.LogInformation("[GenerateSummary] Step 1: Calling AI summarization...");
        var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
        if (!summarizeResult.Success || string.IsNullOrWhiteSpace(summarizeResult.Summary))
        {
            _logger.LogWarning("[GenerateSummary] Summarization failed for issue {Id}: {Error}", issueId, summarizeResult.Error);
            return;
        }
        _logger.LogInformation("[GenerateSummary] Summarization succeeded via {Provider}/{Model}", summarizeResult.Provider, summarizeResult.Model);

        var enProvider = $"{summarizeResult.Provider}/{summarizeResult.Model}";

        // Send English summary if requested
        if (language is "en" or "both")
        {
            _logger.LogInformation("[GenerateSummary] Sending English summary via SignalR...");
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider, "en"),
                cancellationToken);
        }

        // If only English requested, finish
        if (language == "en")
        {
            _logger.LogInformation("[GenerateSummary] COMPLETE (EN only) for issue {Id}", issueId);
            return;
        }

        // Step 2: Translate to Czech
        _logger.LogInformation("[GenerateSummary] Step 2: Calling AI translation...");
        var translateResult = await _translationService.TranslateToCzechAsync(summarizeResult.Summary, cancellationToken);

        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            var csProvider = $"{enProvider} → {translateResult.Provider}/{translateResult.Model}";
            _logger.LogInformation("[GenerateSummary] Translation succeeded via {Provider}/{Model}", translateResult.Provider, translateResult.Model);

            // Send Czech summary
            _logger.LogInformation("[GenerateSummary] Sending Czech summary via SignalR...");
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, translateResult.Translation, csProvider, "cs"),
                cancellationToken);

            _logger.LogInformation("[GenerateSummary] COMPLETE for issue {Id} via {Provider}", issueId, csProvider);
        }
        else
        {
            // Translation failed - use English summary as fallback
            _logger.LogWarning("[GenerateSummary] Translation failed for issue {Id}: {Error}. Using English summary.", issueId, translateResult.Error);

            // If we haven't sent English yet (cs-only mode), send it now as fallback
            if (language == "cs")
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider + " (EN fallback)", "en"),
                    cancellationToken);
            }

            _logger.LogInformation("[GenerateSummary] COMPLETE (EN fallback) for issue {Id}", issueId);
        }
    }
}
