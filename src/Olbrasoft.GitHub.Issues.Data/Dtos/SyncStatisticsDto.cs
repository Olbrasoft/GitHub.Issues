namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Statistics about a sync operation for display in the UI.
/// </summary>
public class SyncStatisticsDto
{
    /// <summary>
    /// Number of API calls made during sync.
    /// </summary>
    public int ApiCalls { get; set; }

    /// <summary>
    /// Total number of issues found from GitHub.
    /// </summary>
    public int TotalFound { get; set; }

    /// <summary>
    /// Number of newly created issues.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Number of updated issues.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Number of unchanged issues.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// The timestamp used for incremental sync (null if full sync).
    /// </summary>
    public DateTimeOffset? SinceTimestamp { get; set; }

    /// <summary>
    /// Combines statistics from another SyncStatisticsDto.
    /// </summary>
    public void Add(SyncStatisticsDto other)
    {
        ApiCalls += other.ApiCalls;
        TotalFound += other.TotalFound;
        Created += other.Created;
        Updated += other.Updated;
        Unchanged += other.Unchanged;
    }
}
