using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

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
    private readonly IAiSummarizationService _summarizationService;
    private readonly IAiTranslationService _translationService;
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly ILogger<IssueDetailService> _logger;

    // Cache validity period - regenerate summary if issue was updated after cache
    private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromDays(7);

    public IssueDetailService(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        IAiSummarizationService summarizationService,
        IAiTranslationService translationService,
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

        // Check for cached summary - don't generate here, just return what we have
        string? summary = null;
        string? summaryProvider = null;
        var summaryPending = false;

        var isCacheValid = issue.SummaryCachedAt.HasValue &&
                          issue.SummaryCachedAt.Value > issue.GitHubUpdatedAt &&
                          issue.SummaryCachedAt.Value > DateTimeOffset.UtcNow.Subtract(CacheValidityPeriod);

        if (isCacheValid && !string.IsNullOrWhiteSpace(issue.CzechSummary))
        {
            _logger.LogDebug("Using cached Czech summary for issue {Id}", issueId);
            summary = issue.CzechSummary;
            summaryProvider = issue.SummaryProvider;
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            // Summary not cached but body exists - mark as pending for progressive loading
            summaryPending = true;
            _logger.LogDebug("Summary pending for issue {Id} - will be generated via SignalR", issueId);
        }

        return new IssueDetailResult(
            Found: true,
            Issue: issueDto,
            Summary: summary,
            SummaryProvider: summaryProvider,
            SummaryError: null,
            ErrorMessage: null,
            SummaryPending: summaryPending);
    }

    private async Task CacheSummaryAsync(int issueId, string czechSummary, string provider, CancellationToken cancellationToken)
    {
        try
        {
            var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);
            if (issue != null)
            {
                issue.CzechSummary = czechSummary;
                issue.SummaryProvider = provider;
                issue.SummaryCachedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Cached Czech summary for issue {Id}", issueId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache summary for issue {Id}", issueId);
            // Don't fail the request if caching fails
        }
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
    /// </summary>
    public async Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting summary generation for issue {Id}", issueId);

        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            _logger.LogWarning("Issue {Id} not found for summary generation", issueId);
            return;
        }

        // Fetch body from GraphQL
        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);
        string? body = null;

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);
            bodies.TryGetValue((owner, repoName, issue.Number), out body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("No body available for issue {Id} - cannot generate summary", issueId);
            return;
        }

        // Step 1: Summarize in English
        var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
        if (!summarizeResult.Success || string.IsNullOrWhiteSpace(summarizeResult.Summary))
        {
            _logger.LogWarning("Failed to summarize issue {Id}: {Error}", issueId, summarizeResult.Error);
            return;
        }

        // Step 2: Translate to Czech
        string summary;
        string summaryProvider;

        var translateResult = await _translationService.TranslateToCzechAsync(summarizeResult.Summary, cancellationToken);
        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            summary = translateResult.Translation;
            summaryProvider = $"{summarizeResult.Provider}/{summarizeResult.Model} → {translateResult.Provider}/{translateResult.Model}";
        }
        else
        {
            // Translation failed - use English summary as fallback
            _logger.LogWarning("Translation failed for issue {Id}: {Error}. Using English summary.", issueId, translateResult.Error);
            summary = summarizeResult.Summary;
            summaryProvider = $"{summarizeResult.Provider}/{summarizeResult.Model} (EN)";
        }

        // Cache the result
        await CacheSummaryAsync(issueId, summary, summaryProvider, cancellationToken);

        // Send notification via SignalR
        await _summaryNotifier.NotifySummaryReadyAsync(
            new SummaryNotificationDto(issueId, summary, summaryProvider),
            cancellationToken);

        _logger.LogInformation("Summary generated and sent for issue {Id} via {Provider}", issueId, summaryProvider);
    }
}
