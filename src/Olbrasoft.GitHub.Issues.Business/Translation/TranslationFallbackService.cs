using Microsoft.Extensions.Logging;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Translation service with automatic fallback to secondary translator.
/// Uses primary translator (Azure) with fallback to secondary (DeepL).
/// </summary>
public class TranslationFallbackService : ITranslationFallbackService
{
    private readonly ITranslator _primaryTranslator;
    private readonly ITranslator? _fallbackTranslator;
    private readonly ILogger<TranslationFallbackService> _logger;

    public TranslationFallbackService(
        ITranslator primaryTranslator,
        ILogger<TranslationFallbackService> logger,
        ITranslator? fallbackTranslator = null)
    {
        _primaryTranslator = primaryTranslator;
        _fallbackTranslator = fallbackTranslator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TranslationFallbackResult> TranslateWithFallbackAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationFallbackResult(false, null, null, false, "Empty text");
        }

        // Try primary translator
        _logger.LogDebug("Attempting translation with primary translator");
        var primaryResult = await _primaryTranslator.TranslateAsync(text, targetLanguage, sourceLanguage, cancellationToken);

        if (primaryResult.Success && !string.IsNullOrWhiteSpace(primaryResult.Translation))
        {
            _logger.LogDebug("Primary translation succeeded via {Provider}", primaryResult.Provider);
            return new TranslationFallbackResult(
                Success: true,
                Translation: primaryResult.Translation,
                Provider: primaryResult.Provider,
                UsedFallback: false,
                Error: null);
        }

        // Primary failed, try fallback if available
        if (_fallbackTranslator == null)
        {
            _logger.LogWarning("Primary translation failed and no fallback configured: {Error}", primaryResult.Error);
            return new TranslationFallbackResult(
                Success: false,
                Translation: null,
                Provider: null,
                UsedFallback: false,
                Error: primaryResult.Error ?? "Translation failed");
        }

        _logger.LogWarning("Primary translation failed: {Error}. Trying fallback translator...", primaryResult.Error);
        var fallbackResult = await _fallbackTranslator.TranslateAsync(text, targetLanguage, sourceLanguage, cancellationToken);

        if (fallbackResult.Success && !string.IsNullOrWhiteSpace(fallbackResult.Translation))
        {
            _logger.LogInformation("Fallback translation succeeded via {Provider}", fallbackResult.Provider);
            return new TranslationFallbackResult(
                Success: true,
                Translation: fallbackResult.Translation,
                Provider: fallbackResult.Provider,
                UsedFallback: true,
                Error: null);
        }

        _logger.LogWarning("Both primary and fallback translation failed: {Error}", fallbackResult.Error);
        return new TranslationFallbackResult(
            Success: false,
            Translation: null,
            Provider: null,
            UsedFallback: true,
            Error: fallbackResult.Error ?? "All translations failed");
    }
}
