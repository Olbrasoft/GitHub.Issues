namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Configuration for search UI.
/// </summary>
public class SearchSettings
{
    /// <summary>
    /// Default number of results per page. Default: 25
    /// </summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// Available page size options for the UI dropdown. Default: [10, 25, 50]
    /// </summary>
    public int[] PageSizeOptions { get; set; } = [10, 25, 50];
}
