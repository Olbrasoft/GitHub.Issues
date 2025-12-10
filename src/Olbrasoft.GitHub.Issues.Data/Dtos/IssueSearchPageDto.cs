namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Paginated search results DTO.
/// </summary>
public class IssueSearchPageDto
{
    /// <summary>Search results for the current page.</summary>
    public List<IssueSearchResultDto> Results { get; set; } = new();

    /// <summary>Total number of matching issues.</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; set; }
}
