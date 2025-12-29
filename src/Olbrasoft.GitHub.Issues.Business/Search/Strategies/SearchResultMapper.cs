using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Search.Strategies;

/// <summary>
/// Maps DTOs to IssueSearchResult models.
/// </summary>
public static class SearchResultMapper
{
    /// <summary>
    /// Maps an IssueSearchResultDto to an IssueSearchResult.
    /// </summary>
    public static IssueSearchResult MapToSearchResult(IssueSearchResultDto dto, bool isExactMatch, int previewMaxLength)
    {
        var parts = dto.RepositoryFullName.Split('/');
        return new IssueSearchResult
        {
            Id = dto.Id,
            IssueNumber = dto.IssueNumber,
            Title = dto.Title,
            IsOpen = dto.IsOpen,
            Url = dto.Url,
            RepositoryName = dto.RepositoryFullName,
            Owner = parts.Length == 2 ? parts[0] : string.Empty,
            RepoName = parts.Length == 2 ? parts[1] : string.Empty,
            Similarity = dto.Similarity,
            IsExactMatch = isExactMatch,
            Labels = dto.Labels,
            PreviewMaxLength = previewMaxLength
        };
    }
}
