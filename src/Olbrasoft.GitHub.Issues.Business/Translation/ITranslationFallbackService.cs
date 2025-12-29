using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Service for translation with automatic fallback to secondary translator.
/// Single responsibility: Translate text with fault tolerance.
/// </summary>
public interface ITranslationFallbackService
{
    /// <summary>
    /// Translates text to target language, falling back to secondary translator if primary fails.
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="targetLanguage">Target language code (e.g., "cs")</param>
    /// <param name="sourceLanguage">Source language code (e.g., "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result with provider information</returns>
    Task<TranslationFallbackResult> TranslateWithFallbackAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of translation with fallback.
/// </summary>
/// <param name="Success">Whether translation succeeded</param>
/// <param name="Translation">Translated text (null if failed)</param>
/// <param name="Provider">Provider that performed the translation</param>
/// <param name="UsedFallback">Whether fallback translator was used</param>
/// <param name="Error">Error message if failed</param>
public record TranslationFallbackResult(
    bool Success,
    string? Translation,
    string? Provider,
    bool UsedFallback,
    string? Error);
