using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Orchestrator that coordinates the complete summary workflow.
/// Delegates to specialized services following SRP.
/// Part of #310 IssueSummaryService refactoring - FINAL STEP (#320).
/// </summary>
public class IssueSummaryOrchestrator : IIssueSummaryOrchestrator
{
    private readonly ICachedTextRepository _repository;
    private readonly ISummaryCacheService _cacheService;
    private readonly IAiSummarizationService _aiService;
    private readonly ITranslationFallbackService _translationService;
    private readonly ISummaryNotificationService _notificationService;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IIssueDetailQueryService _queryService;
    private readonly ILogger<IssueSummaryOrchestrator> _logger;

    public IssueSummaryOrchestrator(
        ICachedTextRepository repository,
        ISummaryCacheService cacheService,
        IAiSummarizationService aiService,
        ITranslationFallbackService translationService,
        ISummaryNotificationService notificationService,
        IGitHubGraphQLClient graphQLClient,
        IIssueDetailQueryService queryService,
        ILogger<IssueSummaryOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(aiService);
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(graphQLClient);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _cacheService = cacheService;
        _aiService = aiService;
        _translationService = translationService;
        _notificationService = notificationService;
        _graphQLClient = graphQLClient;
        _queryService = queryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSummaryFromBodyAsync(
        int issueId,
        string body,
        string language,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[IssueSummaryOrchestrator] START for issue {IssueId}, language={Language}",
            issueId, language);

        // Get issue for cache freshness validation
        var issue = await _repository.GetIssueByIdAsync(issueId, cancellationToken);
        if (issue == null)
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Issue {IssueId} not found", issueId);
            return;
        }

        // Determine which languages to process
        var processEnglish = language is "en" or "both";
        var processCzech = language is "cs" or "both";

        // 1. Try cache first
        var enLanguageCode = (int)LanguageCode.EnUS;
        var csLanguageCode = (int)LanguageCode.CsCZ;

        var cachedEn = processEnglish
            ? await _cacheService.GetIfFreshAsync(issueId, enLanguageCode, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken)
            : null;

        var cachedCs = processCzech
            ? await _cacheService.GetIfFreshAsync(issueId, csLanguageCode, issue.GitHubUpdatedAt.UtcDateTime, cancellationToken)
            : null;

        // If all requested summaries cached, send them and return
        // Logic: "either we don't need EN OR we have it" AND "either we don't need CS OR we have it"
        if ((!processEnglish || cachedEn != null) && (!processCzech || cachedCs != null))
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] Cache HIT - serving from cache");

            if (processEnglish && cachedEn != null)
            {
                await _notificationService.NotifySummaryAsync(issueId, cachedEn, "cache", "en", cancellationToken);
            }

            if (processCzech && cachedCs != null)
            {
                await _notificationService.NotifySummaryAsync(issueId, cachedCs, "cache", "cs", cancellationToken);
            }

            return;
        }

        // Validate body
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Empty body for issue {IssueId}", issueId);
            return;
        }

        // 2. Generate English summary (or use cached)
        string englishSummary;
        string enProvider;

        if (cachedEn != null)
        {
            englishSummary = cachedEn;
            enProvider = "cache";
            _logger.LogInformation("[IssueSummaryOrchestrator] Using cached English summary");
        }
        else
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] Generating AI summary...");
            var aiResult = await _aiService.GenerateSummaryAsync(body, cancellationToken);

            if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Summary))
            {
                _logger.LogWarning(
                    "[IssueSummaryOrchestrator] AI summarization failed: {Error}",
                    aiResult.Error);
                return;
            }

            englishSummary = aiResult.Summary;
            enProvider = aiResult.Provider ?? "Unknown";
            _logger.LogInformation("[IssueSummaryOrchestrator] AI summarization succeeded via {Provider}", enProvider);

            // Save to cache
            await _cacheService.SaveAsync(issueId, enLanguageCode, englishSummary, cancellationToken);
        }

        // Send English summary if requested
        if (processEnglish)
        {
            await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider, "en", cancellationToken);
        }

        // If only English requested, finish
        if (!processCzech)
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (EN only) for issue {IssueId}", issueId);
            return;
        }

        // 3. Generate Czech summary (or use cached)
        if (cachedCs != null)
        {
            await _notificationService.NotifySummaryAsync(issueId, cachedCs, "cache", "cs", cancellationToken);
            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (CS from cache)");
            return;
        }

        // Translate English → Czech
        _logger.LogInformation("[IssueSummaryOrchestrator] Translating to Czech...");
        var translationResult = await _translationService.TranslateWithFallbackAsync(
            englishSummary,
            "cs",
            "en",
            cancellationToken);

        if (translationResult.Success && !string.IsNullOrWhiteSpace(translationResult.Translation))
        {
            var csProvider = $"{enProvider} → {translationResult.Provider}";
            _logger.LogInformation("[IssueSummaryOrchestrator] Translation succeeded via {Provider}", translationResult.Provider);

            // Save Czech to cache
            await _cacheService.SaveAsync(issueId, csLanguageCode, translationResult.Translation, cancellationToken);

            // Send Czech summary
            await _notificationService.NotifySummaryAsync(issueId, translationResult.Translation, csProvider, "cs", cancellationToken);

            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE for issue {IssueId}", issueId);
        }
        else
        {
            _logger.LogWarning(
                "[IssueSummaryOrchestrator] Translation failed: {Error}. Using English fallback.",
                translationResult.Error);

            // Send English as fallback
            if (language == "cs")
            {
                await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider + " (EN fallback)", "en", cancellationToken);
            }
            else if (language == "both")
            {
                await _notificationService.NotifySummaryAsync(issueId, englishSummary, enProvider + " (překlad nedostupný)", "cs", cancellationToken);
            }

            _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE (EN fallback) for issue {IssueId}", issueId);
        }
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, CancellationToken cancellationToken = default)
    {
        await GenerateSummaryAsync(issueId, "both", cancellationToken);
    }

    /// <inheritdoc />
    public async Task GenerateSummaryAsync(int issueId, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[IssueSummaryOrchestrator] START for issue {Id}, language={Language}", issueId, language);

        // Get issue metadata from database
        var issues = await _queryService.GetIssuesByIdsAsync(new[] { issueId }, cancellationToken);
        var issue = issues.FirstOrDefault();

        if (issue == null)
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] Issue {Id} not found", issueId);
            return;
        }

        _logger.LogInformation("[IssueSummaryOrchestrator] Issue {Id} found: {Title}", issueId, issue.Title);

        // Fetch body from GraphQL
        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);
        string? body = null;

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            _logger.LogInformation("[IssueSummaryOrchestrator] Fetching body from GraphQL for {Owner}/{Repo}#{Number}", owner, repoName, issue.Number);
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);
            bodies.TryGetValue((owner, repoName, issue.Number), out body);
            _logger.LogInformation("[IssueSummaryOrchestrator] Body fetched, length: {Length}", body?.Length ?? 0);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IssueSummaryOrchestrator] No body available for issue {Id} - cannot generate summary", issueId);
            return;
        }

        await GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }

    /// <inheritdoc />
    public async Task GenerateSummariesAsync(
        IEnumerable<(int IssueId, string Body)> issuesWithBodies,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var issues = issuesWithBodies.ToList();
        _logger.LogInformation("[IssueSummaryOrchestrator] Triggering summarization for {Count} issues", issues.Count);

        // Trigger summarization for each issue (sequential to avoid LLM overload)
        foreach (var (issueId, body) in issues)
        {
            try
            {
                await GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[IssueSummaryOrchestrator] Failed to generate summary for issue {IssueId}. Continuing with remaining issues.",
                    issueId);
            }
        }

        _logger.LogInformation("[IssueSummaryOrchestrator] COMPLETE for {Count} issues", issues.Count);
    }
}
