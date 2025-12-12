using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for translating issue titles using dedicated translation services (DeepL, Azure).
/// </summary>
public class TitleTranslationService : ITitleTranslationService
{
    private readonly GitHubDbContext _dbContext;
    private readonly ITranslator _translator;
    private readonly ITitleTranslationNotifier _notifier;
    private readonly ILogger<TitleTranslationService> _logger;

    public TitleTranslationService(
        GitHubDbContext dbContext,
        ITranslator translator,
        ITitleTranslationNotifier notifier,
        ILogger<TitleTranslationService> logger)
    {
        _dbContext = dbContext;
        _translator = translator;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task TranslateTitleAsync(int issueId, string targetLanguage = "cs", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[TitleTranslation] START for issue {Id}, target language: {Lang}", issueId, targetLanguage);

        // Skip translation if target language is English (most titles are already in English)
        if (targetLanguage == "en")
        {
            _logger.LogDebug("[TitleTranslation] Skipping - target language is English (original)");
            return;
        }

        var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);

        if (issue == null)
        {
            _logger.LogWarning("[TitleTranslation] Issue {Id} not found", issueId);
            return;
        }

        // Check if title already looks like target language
        if (targetLanguage == "cs" && LooksLikeCzech(issue.Title))
        {
            _logger.LogInformation("[TitleTranslation] Title already looks Czech for issue {Id}, using as-is", issueId);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "original"),
                cancellationToken);

            return;
        }

        if (targetLanguage == "de" && LooksLikeGerman(issue.Title))
        {
            _logger.LogInformation("[TitleTranslation] Title already looks German for issue {Id}, using as-is", issueId);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "original"),
                cancellationToken);

            return;
        }

        // Translate the title using dedicated translation service
        _logger.LogInformation("[TitleTranslation] Calling translation service for: '{Title}' -> {Lang}", issue.Title, targetLanguage);

        var result = await _translator.TranslateAsync(issue.Title, targetLanguage, null, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Translation))
        {
            _logger.LogWarning("[TitleTranslation] Translation failed for issue {Id}: {Error}", issueId, result.Error);

            // Send notification with original title so the UI can stop showing spinner
            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "failed"),
                cancellationToken);

            return;
        }

        _logger.LogInformation("[TitleTranslation] Translation succeeded via {Provider}: '{Translation}'",
            result.Provider, result.Translation);

        // Send notification via SignalR
        await _notifier.NotifyTitleTranslatedAsync(
            new TitleTranslationNotificationDto(issueId, result.Translation, targetLanguage, result.Provider ?? "unknown"),
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

    /// <summary>
    /// Simple heuristic to detect if text might already be in German.
    /// Checks for common German diacritical marks (umlauts, ß).
    /// </summary>
    private static bool LooksLikeGerman(string text)
    {
        // German-specific characters: ä, ö, ü, ß
        var germanChars = new[] { 'ä', 'ö', 'ü', 'ß', 'Ä', 'Ö', 'Ü' };

        // If text contains at least 2 German-specific characters, assume it's German
        var germanCharCount = text.Count(c => germanChars.Contains(c));
        return germanCharCount >= 2;
    }
}
