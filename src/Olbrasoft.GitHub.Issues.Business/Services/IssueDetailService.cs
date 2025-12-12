using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue details including body from GraphQL and AI summary.
/// Uses two-step process: English summarization (LLM) → Translation (DeepL/Azure).
/// Summary generation is progressive - page loads immediately, summary arrives via SignalR.
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ISummarizationService _summarizationService;
    private readonly ITranslator _translator;
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly IBodyNotifier _bodyNotifier;
    private readonly BodyPreviewSettings _bodyPreviewSettings;
    private readonly ILogger<IssueDetailService> _logger;

    public IssueDetailService(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        ISummarizationService summarizationService,
        ITranslator translator,
        ISummaryNotifier summaryNotifier,
        IBodyNotifier bodyNotifier,
        IOptions<BodyPreviewSettings> bodyPreviewSettings,
        ILogger<IssueDetailService> logger)
    {
        _dbContext = dbContext;
        _graphQLClient = graphQLClient;
        _summarizationService = summarizationService;
        _translator = translator;
        _summaryNotifier = summaryNotifier;
        _bodyNotifier = bodyNotifier;
        _bodyPreviewSettings = bodyPreviewSettings.Value;
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

        // Step 2: Translate to target language using Translation Service (DeepL/Azure)
        _logger.LogInformation("[GenerateSummary] Step 2: Calling Translation Service...");
        var translateResult = await _translator.TranslateAsync(summarizeResult.Summary, "cs", "en", cancellationToken);

        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            var csProvider = $"{enProvider} → {translateResult.Provider}";
            _logger.LogInformation("[GenerateSummary] Translation succeeded via {Provider}", translateResult.Provider);

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

    /// <summary>
    /// Generates AI summary from a pre-fetched body and sends notification via SignalR.
    /// Avoids re-fetching body from GraphQL when we already have it.
    /// </summary>
    public async Task GenerateSummaryFromBodyAsync(int issueId, string body, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GenerateSummaryFromBody] START for issue {Id}, language={Language}", issueId, language);

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[GenerateSummaryFromBody] Empty body for issue {Id} - cannot generate summary", issueId);
            return;
        }

        // Step 1: Summarize in English
        _logger.LogInformation("[GenerateSummaryFromBody] Calling AI summarization...");
        var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
        if (!summarizeResult.Success || string.IsNullOrWhiteSpace(summarizeResult.Summary))
        {
            _logger.LogWarning("[GenerateSummaryFromBody] Summarization failed for issue {Id}: {Error}", issueId, summarizeResult.Error);
            return;
        }
        _logger.LogInformation("[GenerateSummaryFromBody] Summarization succeeded via {Provider}/{Model}", summarizeResult.Provider, summarizeResult.Model);

        var enProvider = $"{summarizeResult.Provider}/{summarizeResult.Model}";

        // Send English summary if requested
        if (language is "en" or "both")
        {
            _logger.LogInformation("[GenerateSummaryFromBody] Sending English summary via SignalR...");
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider, "en"),
                cancellationToken);
        }

        // If only English requested, finish
        if (language == "en")
        {
            _logger.LogInformation("[GenerateSummaryFromBody] COMPLETE (EN only) for issue {Id}", issueId);
            return;
        }

        // Step 2: Translate to target language using Translation Service (DeepL/Azure)
        _logger.LogInformation("[GenerateSummaryFromBody] Calling Translation Service...");
        var translateResult = await _translator.TranslateAsync(summarizeResult.Summary, "cs", "en", cancellationToken);

        if (translateResult.Success && !string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            var csProvider = $"{enProvider} → {translateResult.Provider}";
            _logger.LogInformation("[GenerateSummaryFromBody] Translation succeeded via {Provider}", translateResult.Provider);

            // Send translated summary
            _logger.LogInformation("[GenerateSummaryFromBody] Sending translated summary via SignalR...");
            await _summaryNotifier.NotifySummaryReadyAsync(
                new SummaryNotificationDto(issueId, translateResult.Translation, csProvider, "cs"),
                cancellationToken);

            _logger.LogInformation("[GenerateSummaryFromBody] COMPLETE for issue {Id} via {Provider}", issueId, csProvider);
        }
        else
        {
            // Translation failed - use English summary as fallback
            _logger.LogWarning("[GenerateSummaryFromBody] Translation failed for issue {Id}: {Error}. Using English summary.", issueId, translateResult.Error);

            // If we haven't sent English yet (cs-only mode), send it now as fallback
            if (language == "cs")
            {
                await _summaryNotifier.NotifySummaryReadyAsync(
                    new SummaryNotificationDto(issueId, summarizeResult.Summary, enProvider + " (EN fallback)", "en"),
                    cancellationToken);
            }

            _logger.LogInformation("[GenerateSummaryFromBody] COMPLETE (EN fallback) for issue {Id}", issueId);
        }
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
        var issues = await _dbContext.Issues
            .Include(i => i.Repository)
            .Where(i => idList.Contains(i.Id))
            .ToListAsync(cancellationToken);

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
                var preview = CreateBodyPreview(body, _bodyPreviewSettings.MaxLength);
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

        // Trigger summarization for each issue (fire-and-forget, sequential to avoid LLM overload)
        foreach (var (issueId, body) in issuesWithBodies)
        {
            try
            {
                await GenerateSummaryFromBodyAsync(issueId, body, language, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FetchBodies] Summarization failed for issue {Id}", issueId);
            }
        }

        _logger.LogInformation("[FetchBodies] COMPLETE for {Count} issues", idList.Count);
    }

    /// <summary>
    /// Creates a preview of the body text - strips markdown and truncates.
    /// </summary>
    private static string CreateBodyPreview(string body, int maxLength)
    {
        // Strip common markdown patterns
        var text = body;

        // Remove code blocks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", " ", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`[^`]+`", " ");

        // Remove headers
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^#+\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove links but keep text: [text](url) -> text
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");

        // Remove images: ![alt](url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[[^\]]*\]\([^)]+\)", "");

        // Remove bold/italic markers
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*([^*]+)\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"__([^_]+)__", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"_([^_]+)_", "$1");

        // Remove blockquotes
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^>\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove horizontal rules
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^[-*_]{3,}\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Normalize whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = text.Trim();

        // Truncate if needed
        if (text.Length <= maxLength)
        {
            return text;
        }

        // Try to truncate at word boundary
        var truncated = text.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.7)
        {
            truncated = truncated.Substring(0, lastSpace);
        }

        return truncated + "...";
    }
}
