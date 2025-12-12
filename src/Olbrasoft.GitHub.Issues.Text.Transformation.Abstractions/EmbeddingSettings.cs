namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

/// <summary>
/// Configuration for embedding services.
/// Supports both flat legacy config and new hierarchical TextTransformation structure.
/// </summary>
public class EmbeddingSettings
{
    /// <summary>
    /// Which embedding provider to use. Default: Ollama
    /// </summary>
    public EmbeddingProvider Provider { get; set; } = EmbeddingProvider.Ollama;

    /// <summary>
    /// Model name (applies to current provider).
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Vector dimensions for the embedding model.
    /// Ollama nomic-embed-text: 768, Cohere embed-multilingual-v3.0: 1024
    /// </summary>
    public int Dimensions { get; set; } = 768;

    // === Nested provider-specific settings (new structure) ===

    /// <summary>
    /// Ollama-specific settings.
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Cohere-specific settings.
    /// </summary>
    public CohereEmbeddingSettings Cohere { get; set; } = new();

    // === Legacy flat properties (for backward compatibility) ===

    /// <summary>
    /// Legacy: Ollama base URL. Use Ollama.BaseUrl instead.
    /// </summary>
    public string OllamaBaseUrl
    {
        get => Ollama.BaseUrl;
        set => Ollama.BaseUrl = value;
    }

    /// <summary>
    /// Legacy: Ollama model. Use Model instead.
    /// </summary>
    public string OllamaModel
    {
        get => Model;
        set => Model = value;
    }

    /// <summary>
    /// Maximum retries when waiting for Ollama to start. Default: 30 (30 seconds)
    /// </summary>
    public int MaxStartupRetries
    {
        get => Ollama.MaxStartupRetries;
        set => Ollama.MaxStartupRetries = value;
    }

    /// <summary>
    /// Delay between startup retry attempts in milliseconds. Default: 1000 (1 second)
    /// </summary>
    public int StartupRetryDelayMs
    {
        get => Ollama.StartupRetryDelayMs;
        set => Ollama.StartupRetryDelayMs = value;
    }

    /// <summary>
    /// Legacy: Cohere API keys. Use Cohere.ApiKeys instead.
    /// </summary>
    public string[] CohereApiKeys
    {
        get => Cohere.ApiKeys;
        set => Cohere.ApiKeys = value;
    }

    /// <summary>
    /// Legacy: Single Cohere API key.
    /// </summary>
    public string? CohereApiKey { get; set; }

    /// <summary>
    /// Legacy: Cohere model. Use Cohere.Model instead.
    /// </summary>
    public string CohereModel
    {
        get => Cohere.Model;
        set => Cohere.Model = value;
    }

    /// <summary>
    /// Legacy: maps to Ollama.BaseUrl for backward compatibility.
    /// </summary>
    public string BaseUrl
    {
        get => Ollama.BaseUrl;
        set => Ollama.BaseUrl = value;
    }

    /// <summary>
    /// Gets all configured Cohere API keys (combines array and legacy single key).
    /// </summary>
    public IReadOnlyList<string> GetCohereApiKeys()
    {
        var keys = new List<string>();

        if (Cohere.ApiKeys.Length > 0)
        {
            keys.AddRange(Cohere.ApiKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
        }

        // Add legacy single key if array is empty
        if (keys.Count == 0 && !string.IsNullOrWhiteSpace(CohereApiKey))
        {
            keys.Add(CohereApiKey);
        }

        return keys;
    }
}

/// <summary>
/// Ollama-specific embedding settings.
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Base URL for Ollama API. Default: http://localhost:11434
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Vector dimensions. Default: 768 (nomic-embed-text)
    /// </summary>
    public int Dimensions { get; set; } = 768;

    /// <summary>
    /// Maximum retries when waiting for Ollama to start. Default: 30
    /// </summary>
    public int MaxStartupRetries { get; set; } = 30;

    /// <summary>
    /// Delay between startup retry attempts in milliseconds. Default: 1000
    /// </summary>
    public int StartupRetryDelayMs { get; set; } = 1000;
}

/// <summary>
/// Cohere-specific embedding settings.
/// </summary>
public class CohereEmbeddingSettings
{
    /// <summary>
    /// Cohere embedding model. Default: embed-multilingual-v3.0 (1024 dimensions)
    /// </summary>
    public string Model { get; set; } = "embed-multilingual-v3.0";

    /// <summary>
    /// Vector dimensions. Default: 1024
    /// </summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>
    /// API keys to rotate through. Store in User Secrets!
    /// </summary>
    public string[] ApiKeys { get; set; } = [];
}
