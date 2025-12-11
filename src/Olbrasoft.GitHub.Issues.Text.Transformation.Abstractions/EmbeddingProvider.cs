namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

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
