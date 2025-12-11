namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

/// <summary>
/// Interface for services that need lifecycle management (e.g., starting external dependencies).
/// Extracted from IEmbeddingService to follow Interface Segregation Principle (ISP).
/// </summary>
public interface IServiceLifecycleManager
{
    /// <summary>
    /// Ensures the external service is running and available.
    /// </summary>
    Task EnsureRunningAsync(CancellationToken cancellationToken = default);
}
