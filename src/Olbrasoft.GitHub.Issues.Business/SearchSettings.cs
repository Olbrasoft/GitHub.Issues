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
    /// Available page size options for the UI dropdown.
    /// Configured in appsettings.json. Falls back to [10, 25, 50] if not configured.
    /// </summary>
    public int[] PageSizeOptions { get; set; } = [];
}
