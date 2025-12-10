using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Generic embedding service interface following Interface Segregation Principle (ISP).
/// Can be implemented by Ollama, OpenAI, Azure OpenAI, or other embedding providers.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a vector embedding for the given text.
    /// </summary>
    Task<Vector?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the embedding service is available and responding.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the service is properly configured.
    /// </summary>
    bool IsConfigured { get; }
}
