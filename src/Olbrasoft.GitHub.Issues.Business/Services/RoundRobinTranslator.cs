using Microsoft.Extensions.Logging;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Round-robin translator that strictly alternates between providers
/// while rotating keys within each provider.
///
/// With 1 Azure key and 2 DeepL keys:
/// Request 1: Azure-Key1
/// Request 2: DeepL-Key1
/// Request 3: Azure-Key1 (only one key)
/// Request 4: DeepL-Key2
/// Request 5: Azure-Key1
/// Request 6: DeepL-Key1 (repeat)
///
/// Fallback: If Azure fails → try DeepL-1, if DeepL-1 fails → try DeepL-2
/// </summary>
public class RoundRobinTranslator : ITranslator
{
    private readonly IReadOnlyList<ProviderGroup> _providers;
    private readonly ILogger<RoundRobinTranslator> _logger;
    private long _providerIndex;

    /// <summary>
    /// Creates a round-robin translator from grouped translators.
    /// </summary>
    /// <param name="providerGroups">Groups of translators by provider name</param>
    /// <param name="logger">Logger</param>
    public RoundRobinTranslator(
        IEnumerable<ProviderGroup> providerGroups,
        ILogger<RoundRobinTranslator> logger)
    {
        _providers = providerGroups.Where(p => p.Translators.Count > 0).ToList();
        _logger = logger;
        _providerIndex = -1; // Will be incremented to 0 on first call

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one translator must be provided", nameof(providerGroups));
        }

        _logger.LogInformation(
            "[RoundRobinTranslator] Initialized with {ProviderCount} providers: {Providers}",
            _providers.Count,
            string.Join(", ", _providers.Select(p => $"{p.Name}({p.Translators.Count} keys)")));
    }

    /// <summary>
    /// Creates a round-robin translator from a flat list (legacy support).
    /// All translators will be treated as a single provider group.
    /// </summary>
    public RoundRobinTranslator(
        IEnumerable<ITranslator> translators,
        ILogger<RoundRobinTranslator> logger)
        : this(new[] { new ProviderGroup("Default", translators.ToList()) }, logger)
    {
    }

    /// <summary>
    /// Total number of translators across all providers.
    /// </summary>
    public int TranslatorCount => _providers.Sum(p => p.Translators.Count);

    /// <summary>
    /// Number of providers in the pool.
    /// </summary>
    public int ProviderCount => _providers.Count;

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

        // Get next provider (strict alternation)
        var startProviderIndex = (int)(Interlocked.Increment(ref _providerIndex) % _providers.Count);

        _logger.LogDebug(
            "[RoundRobinTranslator] Starting translation, provider index: {Index}/{Count}",
            startProviderIndex, _providers.Count);

        var attemptedProviders = new List<string>();
        var totalTranslators = TranslatorCount;
        var attemptCount = 0;

        // Try all providers and all their keys
        for (int providerOffset = 0; providerOffset < _providers.Count; providerOffset++)
        {
            var providerIndex = (startProviderIndex + providerOffset) % _providers.Count;
            var provider = _providers[providerIndex];

            // Get next key for this provider (rotate within provider)
            var keyIndex = provider.GetNextKeyIndex();

            // On first provider, use its selected key
            // On fallback providers, try all their keys
            var keysToTry = providerOffset == 0 ? 1 : provider.Translators.Count;

            for (int keyOffset = 0; keyOffset < keysToTry; keyOffset++)
            {
                var actualKeyIndex = (keyIndex + keyOffset) % provider.Translators.Count;
                var translator = provider.Translators[actualKeyIndex];
                attemptCount++;

                try
                {
                    var providerKeyName = $"{provider.Name}[{actualKeyIndex}]";

                    _logger.LogDebug(
                        "[RoundRobinTranslator] Attempt {Attempt}/{Total}: {Provider}",
                        attemptCount, totalTranslators, providerKeyName);

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
                    var providerName = result.Provider ?? providerKeyName;
                    attemptedProviders.Add(providerName);

                    _logger.LogWarning(
                        "[RoundRobinTranslator] {Provider} failed: {Error}. Trying next...",
                        providerName, result.Error);
                }
                catch (Exception ex)
                {
                    attemptedProviders.Add($"{provider.Name}[{actualKeyIndex}]");

                    _logger.LogWarning(
                        ex,
                        "[RoundRobinTranslator] {Provider}[{KeyIndex}] threw exception. Trying next...",
                        provider.Name, actualKeyIndex);
                }
            }
        }

        // All translators failed
        var errorMessage = $"All {totalTranslators} translators failed. Attempted: {string.Join(", ", attemptedProviders)}";
        _logger.LogError("[RoundRobinTranslator] {Error}", errorMessage);

        return TranslatorResult.Fail(errorMessage, "RoundRobin");
    }
}

/// <summary>
/// Represents a group of translators for a single provider (e.g., Azure, DeepL).
/// Maintains its own key rotation index.
/// </summary>
public class ProviderGroup
{
    private long _keyIndex = -1; // Will be incremented to 0 on first call

    public string Name { get; }
    public IReadOnlyList<ITranslator> Translators { get; }

    public ProviderGroup(string name, IReadOnlyList<ITranslator> translators)
    {
        Name = name;
        Translators = translators;
    }

    /// <summary>
    /// Gets the next key index for this provider (atomic, thread-safe).
    /// </summary>
    public int GetNextKeyIndex()
    {
        return (int)(Interlocked.Increment(ref _keyIndex) % Translators.Count);
    }
}
