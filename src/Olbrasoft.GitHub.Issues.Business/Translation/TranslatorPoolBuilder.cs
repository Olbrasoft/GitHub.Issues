using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.Bing;
using Olbrasoft.Text.Translation.DeepL;
using Olbrasoft.Text.Translation.Google;

namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Builds provider groups of translators for strict provider alternation.
///
/// With 1 Azure key and 2 DeepL keys, creates:
/// - Azure group: [Azure-Key1]
/// - DeepL group: [DeepL-Key1, DeepL-Key2]
///
/// RoundRobinTranslator then alternates between providers:
/// Request 1: Azure-Key1
/// Request 2: DeepL-Key1
/// Request 3: Azure-Key1
/// Request 4: DeepL-Key2
/// </summary>
public class TranslatorPoolBuilder
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TranslatorPoolSettings _settings;
    private readonly ILoggerFactory _loggerFactory;

    public TranslatorPoolBuilder(
        IHttpClientFactory httpClientFactory,
        IOptions<TranslatorPoolSettings> settings,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Builds provider groups for strict provider alternation in configured order.
    /// </summary>
    public IReadOnlyList<ProviderGroup> BuildProviderGroups()
    {
        var logger = _loggerFactory.CreateLogger<TranslatorPoolBuilder>();

        var azureKeys = _settings.AzureApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var deepLKeys = _settings.DeepLApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        logger.LogInformation(
            "[TranslatorPoolBuilder] Building provider groups: {AzureCount} Azure keys, {DeepLCount} DeepL keys, Google: {GoogleEnabled}, Bing: {BingEnabled}",
            azureKeys.Count, deepLKeys.Count, _settings.GoogleEnabled, _settings.BingEnabled);

        logger.LogInformation(
            "[TranslatorPoolBuilder] Provider order: {Order}",
            string.Join(" → ", _settings.ProviderOrder));

        // Build provider groups based on configured order
        var providerGroups = new Dictionary<string, ProviderGroup>(StringComparer.OrdinalIgnoreCase);

        // Create Azure provider group
        if (azureKeys.Count > 0)
        {
            var azureTranslators = new List<ITranslator>();
            for (int i = 0; i < azureKeys.Count; i++)
            {
                var translator = CreateAzureTranslator(azureKeys[i], i);
                azureTranslators.Add(translator);
                logger.LogDebug("[TranslatorPoolBuilder] Created Azure translator [{Index}]", i);
            }

            providerGroups["Azure"] = new ProviderGroup("Azure", azureTranslators);
            logger.LogInformation("[TranslatorPoolBuilder] Azure group: {Count} translator(s)", azureTranslators.Count);
        }

        // Create DeepL provider group
        if (deepLKeys.Count > 0)
        {
            var deepLTranslators = new List<ITranslator>();
            for (int i = 0; i < deepLKeys.Count; i++)
            {
                var translator = CreateDeepLTranslator(deepLKeys[i], i);
                deepLTranslators.Add(translator);
                logger.LogDebug("[TranslatorPoolBuilder] Created DeepL translator [{Index}]", i);
            }

            providerGroups["DeepL"] = new ProviderGroup("DeepL", deepLTranslators);
            logger.LogInformation("[TranslatorPoolBuilder] DeepL group: {Count} translator(s)", deepLTranslators.Count);
        }

        // Create Google provider group (no API key required)
        if (_settings.GoogleEnabled)
        {
            var googleTranslator = CreateGoogleTranslator();
            providerGroups["Google"] = new ProviderGroup("Google", new List<ITranslator> { googleTranslator });
            logger.LogInformation("[TranslatorPoolBuilder] Google group: 1 translator (free, no API key)");
        }

        // Create Bing provider group (no API key required)
        if (_settings.BingEnabled)
        {
            var bingTranslator = CreateBingTranslator();
            providerGroups["Bing"] = new ProviderGroup("Bing", new List<ITranslator> { bingTranslator });
            logger.LogInformation("[TranslatorPoolBuilder] Bing group: 1 translator (free, no API key)");
        }

        // Arrange groups according to configured order
        var orderedGroups = new List<ProviderGroup>();
        foreach (var providerName in _settings.ProviderOrder)
        {
            if (providerGroups.TryGetValue(providerName, out var group))
            {
                orderedGroups.Add(group);
                logger.LogDebug("[TranslatorPoolBuilder] Added {Provider} to position {Position}", providerName, orderedGroups.Count);
            }
            else
            {
                logger.LogWarning("[TranslatorPoolBuilder] Provider {Provider} in order config but not configured/enabled", providerName);
            }
        }

        var totalTranslators = orderedGroups.Sum(g => g.Translators.Count);
        logger.LogInformation(
            "[TranslatorPoolBuilder] Built {GroupCount} provider groups with {TotalCount} translators in order: {Order}",
            orderedGroups.Count, totalTranslators, string.Join(" → ", orderedGroups.Select(g => g.Name)));

        return orderedGroups;
    }

    /// <summary>
    /// Legacy method - builds an interleaved flat list.
    /// </summary>
    [Obsolete("Use BuildProviderGroups() instead for strict provider alternation")]
    public IReadOnlyList<ITranslator> BuildInterleavedTranslators()
    {
        var groups = BuildProviderGroups();
        var translators = new List<ITranslator>();

        var maxKeyCount = groups.Max(g => g.Translators.Count);

        for (int keyIndex = 0; keyIndex < maxKeyCount; keyIndex++)
        {
            foreach (var group in groups)
            {
                if (keyIndex < group.Translators.Count)
                {
                    translators.Add(group.Translators[keyIndex]);
                }
            }
        }

        return translators;
    }

    private AzureTranslator CreateAzureTranslator(string apiKey, int keyIndex)
    {
        var httpClient = _httpClientFactory.CreateClient($"AzureTranslator_{keyIndex}");
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var settings = Options.Create(new AzureTranslatorSettings
        {
            ApiKey = apiKey,
            Region = _settings.AzureRegion,
            Endpoint = _settings.AzureEndpoint
        });

        var logger = _loggerFactory.CreateLogger<AzureTranslator>();

        return new AzureTranslator(httpClient, settings, logger);
    }

    private DeepLTranslator CreateDeepLTranslator(string apiKey, int keyIndex)
    {
        var httpClient = _httpClientFactory.CreateClient($"DeepLTranslator_{keyIndex}");
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var endpoint = _settings.GetDeepLEndpointForKey(apiKey);

        var settings = Options.Create(new DeepLSettings
        {
            ApiKey = apiKey,
            Endpoint = endpoint
        });

        var logger = _loggerFactory.CreateLogger<DeepLTranslator>();

        return new DeepLTranslator(httpClient, settings, logger);
    }

    private GoogleFreeTranslator CreateGoogleTranslator()
    {
        var settings = Options.Create(new GoogleFreeTranslatorSettings
        {
            TimeoutSeconds = _settings.GoogleTimeoutSeconds
        });

        var logger = _loggerFactory.CreateLogger<GoogleFreeTranslator>();

        return new GoogleFreeTranslator(settings, logger);
    }

    private BingFreeTranslator CreateBingTranslator()
    {
        var settings = Options.Create(new BingFreeTranslatorSettings
        {
            TimeoutSeconds = _settings.BingTimeoutSeconds
        });

        var logger = _loggerFactory.CreateLogger<BingFreeTranslator>();

        return new BingFreeTranslator(settings, logger);
    }
}
