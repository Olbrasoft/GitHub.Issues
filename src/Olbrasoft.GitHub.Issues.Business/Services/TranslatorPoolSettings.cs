namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Settings for the round-robin translator pool.
/// Supports multiple API keys per provider for load distribution.
/// </summary>
public class TranslatorPoolSettings
{
    /// <summary>
    /// Azure Translator API keys. Each key creates a separate translator instance.
    /// </summary>
    public string[] AzureApiKeys { get; set; } = [];

    /// <summary>
    /// Azure Translator region (shared by all keys).
    /// </summary>
    public string AzureRegion { get; set; } = "westeurope";

    /// <summary>
    /// Azure Translator endpoint (shared by all keys).
    /// </summary>
    public string AzureEndpoint { get; set; } = "https://api.cognitive.microsofttranslator.com/";

    /// <summary>
    /// DeepL API keys. Each key creates a separate translator instance.
    /// Keys ending with ":fx" use the free tier endpoint.
    /// </summary>
    public string[] DeepLApiKeys { get; set; } = [];

    /// <summary>
    /// DeepL API endpoint for paid accounts.
    /// Free accounts (keys ending with ":fx") automatically use api-free.deepl.com.
    /// </summary>
    public string DeepLEndpoint { get; set; } = "https://api.deepl.com/v2/";

    /// <summary>
    /// DeepL API endpoint for free accounts.
    /// </summary>
    public string DeepLFreeEndpoint { get; set; } = "https://api-free.deepl.com/v2/";

    /// <summary>
    /// Returns true if any translator keys are configured.
    /// </summary>
    public bool HasAnyTranslators =>
        AzureApiKeys.Any(k => !string.IsNullOrWhiteSpace(k)) ||
        DeepLApiKeys.Any(k => !string.IsNullOrWhiteSpace(k));

    /// <summary>
    /// Returns the appropriate DeepL endpoint for the given API key.
    /// Free tier keys (ending with ":fx") use the free endpoint.
    /// </summary>
    public string GetDeepLEndpointForKey(string apiKey)
    {
        return apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
            ? DeepLFreeEndpoint
            : DeepLEndpoint;
    }
}
