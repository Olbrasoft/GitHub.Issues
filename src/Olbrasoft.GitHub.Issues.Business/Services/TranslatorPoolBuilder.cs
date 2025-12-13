using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.DeepL;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Builds an interleaved pool of translators for round-robin load distribution.
///
/// Interleaving pattern (with 2 Azure keys, 2 DeepL keys):
/// [0] Azure-Key1
/// [1] DeepL-Key1
/// [2] Azure-Key2
/// [3] DeepL-Key2
///
/// This ensures requests alternate between providers, distributing load evenly.
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
    /// Builds an interleaved list of translators.
    /// Alternates between Azure and DeepL translators for each key index.
    /// </summary>
    public IReadOnlyList<ITranslator> BuildInterleavedTranslators()
    {
        var azureKeys = _settings.AzureApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var deepLKeys = _settings.DeepLApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var maxKeyCount = Math.Max(azureKeys.Count, deepLKeys.Count);
        var translators = new List<ITranslator>();

        var logger = _loggerFactory.CreateLogger<TranslatorPoolBuilder>();
        logger.LogInformation(
            "[TranslatorPoolBuilder] Building pool with {AzureCount} Azure keys, {DeepLCount} DeepL keys",
            azureKeys.Count, deepLKeys.Count);

        // Interleave: for each key index, add Azure first, then DeepL
        for (int keyIndex = 0; keyIndex < maxKeyCount; keyIndex++)
        {
            // Add Azure translator for this key index (if available)
            if (keyIndex < azureKeys.Count)
            {
                var azureTranslator = CreateAzureTranslator(azureKeys[keyIndex], keyIndex);
                translators.Add(azureTranslator);

                logger.LogDebug(
                    "[TranslatorPoolBuilder] Added Azure translator [{Index}] at position {Position}",
                    keyIndex, translators.Count - 1);
            }

            // Add DeepL translator for this key index (if available)
            if (keyIndex < deepLKeys.Count)
            {
                var deepLTranslator = CreateDeepLTranslator(deepLKeys[keyIndex], keyIndex);
                translators.Add(deepLTranslator);

                logger.LogDebug(
                    "[TranslatorPoolBuilder] Added DeepL translator [{Index}] at position {Position}",
                    keyIndex, translators.Count - 1);
            }
        }

        logger.LogInformation(
            "[TranslatorPoolBuilder] Pool built with {Total} translators",
            translators.Count);

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
