namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Configuration for GitHub sync operations.
/// </summary>
public class SyncSettings
{
    /// <summary>
    /// Number of items per GitHub API page request. Default: 100 (GitHub max)
    /// </summary>
    public int GitHubApiPageSize { get; set; } = 100;

    /// <summary>
    /// Number of events to process before saving to database. Default: 100
    /// </summary>
    public int BatchSaveSize { get; set; } = 100;

    /// <summary>
    /// Maximum text length for embedding generation (truncates longer text).
    /// Default: 8000 (conservative limit for nomic-embed-text ~8192 token max)
    /// </summary>
    public int MaxEmbeddingTextLength { get; set; } = 8000;
}
