namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Generates embeddings for GitHub issues by fetching comments and building embedding text.
/// Single responsibility: Coordinate embedding generation workflow.
/// </summary>
public interface IIssueEmbeddingGenerator
{
    /// <summary>
    /// Generates an embedding for an issue, including fetching comments from GitHub API.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="title">Issue title</param>
    /// <param name="body">Issue body (optional)</param>
    /// <param name="labelNames">List of label names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated embedding or null if generation failed</returns>
    Task<float[]?> GenerateEmbeddingAsync(
        string owner,
        string repo,
        int issueNumber,
        string title,
        string? body,
        IReadOnlyList<string> labelNames,
        CancellationToken cancellationToken = default);
}
