namespace Olbrasoft.GitHub.Issues.Business.Translation;

/// <summary>
/// Settings for the round-robin translator pool.
/// Supports multiple API keys per provider for load distribution.
/// </summary>
public class TranslatorPoolSettings
{
    /// <summary>
    /// Provider order for fallback chain. Default: ["Azure", "DeepL", "Google"]
    /// Example: ["Google", "Azure", "DeepL"] - tries Google first, then Azure, then DeepL
    /// </summary>
    public string[] ProviderOrder { get; set; } = ["Azure", "DeepL", "Google"];

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
    /// Enable Google Translate free service (unofficial API, no key required).
    /// Default: true (enabled)
    /// </summary>
    public bool GoogleEnabled { get; set; } = true;

    /// <summary>
    /// Timeout in seconds for Google Translate requests. Default: 10 seconds.
    /// </summary>
    public int GoogleTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Enable Bing Translate free service (unofficial API, no key required).
    /// Default: false (disabled)
    /// </summary>
    public bool BingEnabled { get; set; } = false;

    /// <summary>
    /// Timeout in seconds for Bing Translate requests. Default: 10 seconds.
    /// </summary>
    public int BingTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Returns true if any translator is configured (Azure, DeepL, Google, or Bing).
    /// </summary>
    public bool HasAnyTranslators =>
        AzureApiKeys.Any(k => !string.IsNullOrWhiteSpace(k)) ||
        DeepLApiKeys.Any(k => !string.IsNullOrWhiteSpace(k)) ||
        GoogleEnabled ||
        BingEnabled;

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
