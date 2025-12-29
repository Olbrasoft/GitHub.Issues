namespace Olbrasoft.GitHub.Issues.Data;

/// <summary>
/// Statistics about cached translations and summaries.
/// </summary>
public record CacheStatistics
{
    /// <summary>
    /// Total number of cached text entries.
    /// </summary>
    public int TotalRecords { get; init; }

    /// <summary>
    /// Count of cached entries grouped by language culture name.
    /// </summary>
    public Dictionary<string, int> ByLanguage { get; init; } = new();

    /// <summary>
    /// Count of cached entries grouped by text type name.
    /// </summary>
    public Dictionary<string, int> ByTextType { get; init; } = new();
}
