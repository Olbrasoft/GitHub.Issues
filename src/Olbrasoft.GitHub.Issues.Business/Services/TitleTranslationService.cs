using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for translating issue titles to Czech.
/// Uses AI translation with caching to avoid repeated API calls.
/// </summary>
public class TitleTranslationService : ITitleTranslationService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IAiTranslationService _translationService;
    private readonly ITitleTranslationNotifier _notifier;
    private readonly ILogger<TitleTranslationService> _logger;

    // Cache validity period - regenerate translation if issue was updated after cache
    private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromDays(30);

    public TitleTranslationService(
        GitHubDbContext dbContext,
        IAiTranslationService translationService,
        ITitleTranslationNotifier notifier,
        ILogger<TitleTranslationService> logger)
    {
        _dbContext = dbContext;
        _translationService = translationService;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task TranslateTitleAsync(int issueId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[TitleTranslation] START for issue {Id}", issueId);

        var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);

        if (issue == null)
        {
            _logger.LogWarning("[TitleTranslation] Issue {Id} not found", issueId);
            return;
        }

        // Check if we have a valid cached translation
        var isCacheValid = issue.CzechTitleCachedAt.HasValue &&
                          issue.CzechTitleCachedAt.Value > issue.GitHubUpdatedAt &&
                          issue.CzechTitleCachedAt.Value > DateTimeOffset.UtcNow.Subtract(CacheValidityPeriod);

        if (isCacheValid && !string.IsNullOrWhiteSpace(issue.CzechTitle))
        {
            _logger.LogInformation("[TitleTranslation] Using cached translation for issue {Id}", issueId);

            // Send cached translation via SignalR
            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(
                    issueId,
                    issue.CzechTitle,
                    issue.TitleTranslationProvider ?? "cache"),
                cancellationToken);

            return;
        }

        // Check if title is already in Czech (contains Czech-specific characters)
        if (LooksLikeCzech(issue.Title))
        {
            _logger.LogInformation("[TitleTranslation] Title already looks Czech for issue {Id}, using as-is", issueId);

            // Cache it as-is
            issue.CzechTitle = issue.Title;
            issue.TitleTranslationProvider = "original";
            issue.CzechTitleCachedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, "original"),
                cancellationToken);

            return;
        }

        // Translate the title
        _logger.LogInformation("[TitleTranslation] Calling AI translation for: '{Title}'", issue.Title);

        var result = await _translationService.TranslateToCzechAsync(issue.Title, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Translation))
        {
            _logger.LogWarning("[TitleTranslation] Translation failed for issue {Id}: {Error}", issueId, result.Error);
            return;
        }

        var provider = $"{result.Provider}/{result.Model}";
        _logger.LogInformation("[TitleTranslation] Translation succeeded via {Provider}: '{Translation}'",
            provider, result.Translation);

        // Cache the translation
        issue.CzechTitle = result.Translation;
        issue.TitleTranslationProvider = provider;
        issue.CzechTitleCachedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send notification via SignalR
        await _notifier.NotifyTitleTranslatedAsync(
            new TitleTranslationNotificationDto(issueId, result.Translation, provider),
            cancellationToken);

        _logger.LogInformation("[TitleTranslation] COMPLETE for issue {Id}", issueId);
    }

    /// <summary>
    /// Simple heuristic to detect if text might already be in Czech.
    /// Checks for common Czech diacritical marks.
    /// </summary>
    private static bool LooksLikeCzech(string text)
    {
        // Czech-specific characters: ě, š, č, ř, ž, ý, á, í, é, ú, ů, ď, ť, ň
        var czechChars = new[] { 'ě', 'š', 'č', 'ř', 'ž', 'ý', 'á', 'í', 'é', 'ú', 'ů', 'ď', 'ť', 'ň',
                                  'Ě', 'Š', 'Č', 'Ř', 'Ž', 'Ý', 'Á', 'Í', 'É', 'Ú', 'Ů', 'Ď', 'Ť', 'Ň' };

        // If text contains at least 2 Czech-specific characters, assume it's Czech
        var czechCharCount = text.Count(c => czechChars.Contains(c));
        return czechCharCount >= 2;
    }
}
