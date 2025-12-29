using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Service for translating issue titles using dedicated translation services.
/// Primary: Azure Translator. Fallback: DeepL (or any other ITranslator).
/// Note: Cohere (AI-based) removed per Issue #209 - translations must use proper translators.
/// Updated to follow DIP by depending on ITranslationRepository abstraction.
/// </summary>
public class TitleTranslationService : ITitleTranslationService
{
    private readonly ITranslationRepository _translationRepository;
    private readonly ITranslator _translator;
    private readonly ITranslator? _fallbackTranslator;
    private readonly ITitleTranslationNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TitleTranslationService> _logger;

    public TitleTranslationService(
        ITranslationRepository translationRepository,
        ITranslator translator,
        ITitleTranslationNotifier notifier,
        TimeProvider timeProvider,
        ILogger<TitleTranslationService> logger,
        ITranslator? fallbackTranslator = null)
    {
        ArgumentNullException.ThrowIfNull(translationRepository);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _translationRepository = translationRepository;
        _translator = translator;
        _fallbackTranslator = fallbackTranslator;
        _notifier = notifier;
        _timeProvider = timeProvider;
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

        var issue = await _translationRepository.GetIssueByIdAsync(issueId, cancellationToken);

        if (issue == null)
        {
            _logger.LogWarning("[TitleTranslation] Issue {Id} not found", issueId);
            return;
        }

        // Get language LCID for cache lookup
        var languageId = MapLanguageCodeToLcid(targetLanguage);
        if (languageId == 0)
        {
            _logger.LogWarning("[TitleTranslation] Unknown target language: {Lang}", targetLanguage);
            return;
        }

        // Check cache first (with automatic freshness validation)
        var cachedTranslation = await _translationRepository.GetIfFreshAsync(
            issueId,
            languageId,
            (int)TextTypeCode.Title,
            issue.GitHubUpdatedAt.UtcDateTime,
            cancellationToken);

        if (cachedTranslation != null)
        {
            // Cache hit - fresh translation found
            _logger.LogInformation("[TitleTranslation] Cache HIT for issue {Id}, language {Lang}", issueId, targetLanguage);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, cachedTranslation, targetLanguage, "cache"),
                cancellationToken);

            return;
        }

        _logger.LogInformation("[TitleTranslation] Cache MISS for issue {Id}, language {Lang}", issueId, targetLanguage);

        // Check if title already looks like target language
        if (targetLanguage == "cs" && LooksLikeCzech(issue.Title))
        {
            _logger.LogInformation("[TitleTranslation] Title already looks Czech for issue {Id}, using as-is", issueId);

            await SaveToCacheAsync(issueId, languageId, issue.Title, cancellationToken);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "original"),
                cancellationToken);

            return;
        }

        if (targetLanguage == "de" && LooksLikeGerman(issue.Title))
        {
            _logger.LogInformation("[TitleTranslation] Title already looks German for issue {Id}, using as-is", issueId);

            await SaveToCacheAsync(issueId, languageId, issue.Title, cancellationToken);

            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "original"),
                cancellationToken);

            return;
        }

        // Translate the title - try primary translator first, then fallback
        _logger.LogInformation("[TitleTranslation] Calling translation service for: '{Title}' -> {Lang}", issue.Title, targetLanguage);

        var result = await _translator.TranslateAsync(issue.Title, targetLanguage, null, cancellationToken);

        // If primary translator fails, try fallback (DeepL)
        if ((!result.Success || string.IsNullOrWhiteSpace(result.Translation)) && _fallbackTranslator != null)
        {
            _logger.LogWarning("[TitleTranslation] Primary translation failed: {Error}. Trying DeepL fallback...", result.Error);

            var fallbackResult = await _fallbackTranslator.TranslateAsync(issue.Title, targetLanguage, null, cancellationToken);
            if (fallbackResult.Success && !string.IsNullOrWhiteSpace(fallbackResult.Translation))
            {
                _logger.LogInformation("[TitleTranslation] Fallback succeeded via {Provider}: '{Translation}'",
                    fallbackResult.Provider, fallbackResult.Translation);

                await SaveToCacheAsync(issueId, languageId, fallbackResult.Translation, cancellationToken);

                await _notifier.NotifyTitleTranslatedAsync(
                    new TitleTranslationNotificationDto(issueId, fallbackResult.Translation, targetLanguage, fallbackResult.Provider ?? "DeepL"),
                    cancellationToken);

                _logger.LogInformation("[TitleTranslation] COMPLETE for issue {Id} (via fallback)", issueId);
                return;
            }

            _logger.LogWarning("[TitleTranslation] Fallback also failed: {Error}", fallbackResult.Error);
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.Translation))
        {
            _logger.LogWarning("[TitleTranslation] All translation attempts failed for issue {Id}: {Error}", issueId, result.Error);

            // Send notification with original title so the UI can stop showing spinner
            await _notifier.NotifyTitleTranslatedAsync(
                new TitleTranslationNotificationDto(issueId, issue.Title, targetLanguage, "failed"),
                cancellationToken);

            return;
        }

        _logger.LogInformation("[TitleTranslation] Translation succeeded via {Provider}: '{Translation}'",
            result.Provider, result.Translation);

        // Save to cache
        await SaveToCacheAsync(issueId, languageId, result.Translation, cancellationToken);

        // Send notification via SignalR
        await _notifier.NotifyTitleTranslatedAsync(
            new TitleTranslationNotificationDto(issueId, result.Translation, targetLanguage, result.Provider ?? "unknown"),
            cancellationToken);

        _logger.LogInformation("[TitleTranslation] COMPLETE for issue {Id}", issueId);
    }

    /// <summary>
    /// Maps language code string to LCID (Language Culture ID).
    /// </summary>
    private static int MapLanguageCodeToLcid(string languageCode) => languageCode switch
    {
        "cs" => (int)LanguageCode.CsCZ,  // 1029
        "de" => (int)LanguageCode.DeDE,  // 1031
        "en" => (int)LanguageCode.EnUS,  // 1033
        _ => 0
    };

    /// <summary>
    /// Saves translation to the cache table.
    /// Note: Duplicate key exception handling is in repository.
    /// </summary>
    private async Task SaveToCacheAsync(int issueId, int languageId, string content, CancellationToken ct)
    {
        var cachedText = new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = (int)TextTypeCode.Title,
            Content = content,
            CachedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        await _translationRepository.SaveCachedTextAsync(cachedText, ct);
        _logger.LogDebug("[TitleTranslation] Saved to cache: Issue {IssueId}, Language {LanguageId}", issueId, languageId);
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
