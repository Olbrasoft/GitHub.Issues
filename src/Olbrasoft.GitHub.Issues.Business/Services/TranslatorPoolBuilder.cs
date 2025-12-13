using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.DeepL;

namespace Olbrasoft.GitHub.Issues.Business.Services;

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
    /// Builds provider groups for strict provider alternation.
    /// </summary>
    public IReadOnlyList<ProviderGroup> BuildProviderGroups()
    {
        var groups = new List<ProviderGroup>();
        var logger = _loggerFactory.CreateLogger<TranslatorPoolBuilder>();

        var azureKeys = _settings.AzureApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var deepLKeys = _settings.DeepLApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        logger.LogInformation(
            "[TranslatorPoolBuilder] Building provider groups: {AzureCount} Azure keys, {DeepLCount} DeepL keys",
            azureKeys.Count, deepLKeys.Count);

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

            groups.Add(new ProviderGroup("Azure", azureTranslators));
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

            groups.Add(new ProviderGroup("DeepL", deepLTranslators));
            logger.LogInformation("[TranslatorPoolBuilder] DeepL group: {Count} translator(s)", deepLTranslators.Count);
        }

        var totalTranslators = groups.Sum(g => g.Translators.Count);
        logger.LogInformation(
            "[TranslatorPoolBuilder] Built {GroupCount} provider groups with {TotalCount} translators",
            groups.Count, totalTranslators);

        return groups;
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
}
