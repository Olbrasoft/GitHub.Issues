namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Configuration;

/// <summary>
/// Helper for loading API keys from configuration (supports both array and individual key formats).
/// </summary>
public static class ConfigurationKeyLoader
{
    /// <summary>
    /// Loads API keys from configuration using numbered key format (Key1, Key2, etc.).
    /// Stops at first missing key to avoid unnecessary lookups.
    /// </summary>
    /// <param name="configuration">Configuration instance.</param>
    /// <param name="keyPrefix">Key prefix (e.g., "TranslatorPool:AzureApiKey").</param>
    /// <returns>List of non-empty keys found.</returns>
    public static List<string> LoadNumberedKeys(IConfiguration configuration, string keyPrefix)
    {
        var keys = new List<string>();

        for (var i = 1; ; i++)
        {
            var key = configuration[$"{keyPrefix}{i}"];
            if (string.IsNullOrWhiteSpace(key))
            {
                // Stop at first missing or empty key to avoid unnecessary lookups
                break;
            }
            keys.Add(key);
        }

        return keys;
    }
}
