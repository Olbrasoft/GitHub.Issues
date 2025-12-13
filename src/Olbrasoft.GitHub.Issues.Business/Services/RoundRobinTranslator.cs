using Microsoft.Extensions.Logging;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Round-robin translator that rotates between multiple translators (providers/keys).
/// Provides automatic fallback when a translator fails.
///
/// Rotation pattern with 2 providers, 2 keys each:
/// Request 1: Azure-Key1
/// Request 2: DeepL-Key1
/// Request 3: Azure-Key2
/// Request 4: DeepL-Key2
/// Request 5: Azure-Key1 (repeat)
///
/// This distributes load evenly across providers AND keys.
/// </summary>
public class RoundRobinTranslator : ITranslator
{
    private readonly IReadOnlyList<ITranslator> _translators;
    private readonly ILogger<RoundRobinTranslator> _logger;
    private long _currentIndex;

    public RoundRobinTranslator(
        IEnumerable<ITranslator> translators,
        ILogger<RoundRobinTranslator> logger)
    {
        _translators = translators.ToList();
        _logger = logger;
        _currentIndex = 0;

        if (_translators.Count == 0)
        {
            throw new ArgumentException("At least one translator must be provided", nameof(translators));
        }

        _logger.LogInformation(
            "[RoundRobinTranslator] Initialized with {Count} translators",
            _translators.Count);
    }

    /// <summary>
    /// Number of translators in the pool.
    /// </summary>
    public int TranslatorCount => _translators.Count;

    /// <summary>
    /// Current position in the round-robin rotation.
    /// </summary>
    public long CurrentIndex => Interlocked.Read(ref _currentIndex);

    /// <inheritdoc />
    public async Task<TranslatorResult> TranslateAsync(
        string text,
        string targetLanguage,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslatorResult.Fail("Empty text", "RoundRobin");
        }

        // Get next translator index (atomic increment)
        var startIndex = (int)(Interlocked.Increment(ref _currentIndex) % _translators.Count);

        _logger.LogDebug(
            "[RoundRobinTranslator] Starting translation, initial index: {Index}/{Count}",
            startIndex, _translators.Count);

        // Try all translators starting from current position
        var attemptedProviders = new List<string>();

        for (int attempt = 0; attempt < _translators.Count; attempt++)
        {
            var index = (startIndex + attempt) % _translators.Count;
            var translator = _translators[index];

            try
            {
                _logger.LogDebug(
                    "[RoundRobinTranslator] Attempt {Attempt}/{Count} using translator at index {Index}",
                    attempt + 1, _translators.Count, index);

                var result = await translator.TranslateAsync(
                    text, targetLanguage, sourceLanguage, cancellationToken);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Translation))
                {
                    _logger.LogDebug(
                        "[RoundRobinTranslator] Translation succeeded via {Provider}",
                        result.Provider);
                    return result;
                }

                // Translation failed, try next
                var providerName = result.Provider ?? $"Translator[{index}]";
                attemptedProviders.Add(providerName);

                _logger.LogWarning(
                    "[RoundRobinTranslator] Translator {Provider} failed: {Error}. Trying next...",
                    providerName, result.Error);
            }
            catch (Exception ex)
            {
                attemptedProviders.Add($"Translator[{index}]");

                _logger.LogWarning(
                    ex,
                    "[RoundRobinTranslator] Translator at index {Index} threw exception. Trying next...",
                    index);
            }
        }

        // All translators failed
        var errorMessage = $"All {_translators.Count} translators failed. Attempted: {string.Join(", ", attemptedProviders)}";
        _logger.LogError("[RoundRobinTranslator] {Error}", errorMessage);

        return TranslatorResult.Fail(errorMessage, "RoundRobin");
    }
}
