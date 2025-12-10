namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Available embedding providers.
/// </summary>
public enum EmbeddingProvider
{
    /// <summary>
    /// Local Ollama instance (requires Ollama running on localhost).
    /// </summary>
    Ollama,

    /// <summary>
    /// Cohere cloud API (requires API key).
    /// </summary>
    Cohere
}

/// <summary>
/// Configuration for embedding services.
/// </summary>
public class EmbeddingSettings
{
    /// <summary>
    /// Which embedding provider to use. Default: Ollama
    /// </summary>
    public EmbeddingProvider Provider { get; set; } = EmbeddingProvider.Ollama;

    /// <summary>
    /// Vector dimensions for the embedding model.
    /// Ollama nomic-embed-text: 768, Cohere embed-multilingual-v3.0: 1024
    /// </summary>
    public int Dimensions { get; set; } = 768;

    // === Ollama-specific settings ===

    /// <summary>
    /// Base URL for Ollama API. Default: http://localhost:11434
    /// </summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Ollama embedding model name. Default: nomic-embed-text (768 dimensions)
    /// </summary>
    public string OllamaModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Maximum retries when waiting for Ollama to start. Default: 30 (30 seconds)
    /// </summary>
    public int MaxStartupRetries { get; set; } = 30;

    /// <summary>
    /// Delay between startup retry attempts in milliseconds. Default: 1000 (1 second)
    /// </summary>
    public int StartupRetryDelayMs { get; set; } = 1000;

    // === Cohere-specific settings ===

    /// <summary>
    /// Array of Cohere API keys for round-robin rotation.
    /// Store in User Secrets or Azure App Settings, NOT in appsettings.json!
    /// Example: Embeddings:CohereApiKeys:0 = "key1", Embeddings:CohereApiKeys:1 = "key2"
    /// </summary>
    public string[] CohereApiKeys { get; set; } = [];

    /// <summary>
    /// Legacy single Cohere API key. If set and CohereApiKeys is empty, this will be used.
    /// Prefer using CohereApiKeys array for multiple keys.
    /// </summary>
    public string? CohereApiKey { get; set; }

    /// <summary>
    /// Cohere embedding model. Default: embed-multilingual-v3.0 (1024 dimensions, supports Czech)
    /// </summary>
    public string CohereModel { get; set; } = "embed-multilingual-v3.0";

    /// <summary>
    /// Gets all configured Cohere API keys (combines array and legacy single key).
    /// </summary>
    public IReadOnlyList<string> GetCohereApiKeys()
    {
        var keys = new List<string>();

        if (CohereApiKeys.Length > 0)
        {
            keys.AddRange(CohereApiKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
        }

        // Add legacy single key if array is empty
        if (keys.Count == 0 && !string.IsNullOrWhiteSpace(CohereApiKey))
        {
            keys.Add(CohereApiKey);
        }

        return keys;
    }

    // === Legacy property mappings (for backward compatibility) ===

    /// <summary>
    /// Legacy: maps to OllamaBaseUrl for backward compatibility.
    /// </summary>
    public string BaseUrl
    {
        get => OllamaBaseUrl;
        set => OllamaBaseUrl = value;
    }

    /// <summary>
    /// Legacy: maps to OllamaModel for backward compatibility.
    /// </summary>
    public string Model
    {
        get => OllamaModel;
        set => OllamaModel = value;
    }
}
