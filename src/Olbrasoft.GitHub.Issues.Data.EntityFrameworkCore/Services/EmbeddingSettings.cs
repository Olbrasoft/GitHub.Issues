namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Configuration for the embedding service (Ollama).
/// </summary>
public class EmbeddingSettings
{
    /// <summary>
    /// Base URL for Ollama API. Default: http://localhost:11434
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Embedding model name. Default: nomic-embed-text (768 dimensions)
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Maximum retries when waiting for Ollama to start. Default: 30 (30 seconds)
    /// </summary>
    public int MaxStartupRetries { get; set; } = 30;

    /// <summary>
    /// Delay between startup retry attempts in milliseconds. Default: 1000 (1 second)
    /// </summary>
    public int StartupRetryDelayMs { get; set; } = 1000;
}
