using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Detail;

/// <summary>
/// Service for querying issue details from the database.
/// Single responsibility: Database queries only (SRP).
/// Refactored from IssueDetailService to follow Single Responsibility Principle.
/// </summary>
public class IssueDetailQueryService : IIssueDetailQueryService
{
    private readonly IIssueRepository _issueRepository;
    private readonly ILogger<IssueDetailQueryService> _logger;

    public IssueDetailQueryService(
        IIssueRepository issueRepository,
        ILogger<IssueDetailQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(issueRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _issueRepository = issueRepository;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issueRepository.GetIssueWithDetailsAsync(issueId, cancellationToken);

        if (issue == null)
        {
            return new IssueDetailResult(
                Found: false,
                Issue: null,
                Summary: null,
                SummaryProvider: null,
                SummaryError: null,
                ErrorMessage: "Issue nenalezeno.");
        }

        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);

        var labels = issue.IssueLabels
            .Select(il => new LabelDto(il.Label.Name, il.Label.Color))
            .ToList();

        // Fetch sub-issues if this is a parent
        List<SubIssueSummaryDto>? subIssues = null;
        var subIssueEntities = await _issueRepository.GetSubIssuesAsync(issueId, cancellationToken);
        if (subIssueEntities.Count > 0)
        {
            subIssues = subIssueEntities
                .Select(s => new SubIssueSummaryDto(s.Id, s.Number, s.Title, s.IsOpen))
                .ToList();
        }

        // Fetch parent info if this is a sub-issue
        int? parentIssueId = issue.ParentIssueId;
        int? parentIssueNumber = null;
        string? parentIssueTitle = null;
        if (parentIssueId.HasValue)
        {
            var parent = await _issueRepository.GetParentIssueAsync(parentIssueId.Value, cancellationToken);
            if (parent != null)
            {
                parentIssueNumber = parent.Number;
                parentIssueTitle = parent.Title;
            }
        }

        var issueDto = new IssueDetailDto(
            Id: issue.Id,
            IssueNumber: issue.Number,
            Title: issue.Title,
            IsOpen: issue.IsOpen,
            Url: issue.Url,
            Owner: owner,
            RepoName: repoName,
            RepositoryName: issue.Repository.FullName,
            Body: null,
            Labels: labels,
            ParentIssueId: parentIssueId,
            ParentIssueNumber: parentIssueNumber,
            ParentIssueTitle: parentIssueTitle,
            SubIssues: subIssues);

        return new IssueDetailResult(
            Found: true,
            Issue: issueDto,
            Summary: null,
            SummaryProvider: null,
            SummaryError: null,
            ErrorMessage: null,
            SummaryPending: false);
    }

    public async Task<List<Issue>> GetIssuesByIdsAsync(IEnumerable<int> issueIds, CancellationToken cancellationToken = default)
    {
        var idList = issueIds.ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        _logger.LogDebug("[IssueDetailQuery] Getting {Count} issues by IDs", idList.Count);

        return await _issueRepository.GetIssuesByIdsWithRepositoryAsync(idList, cancellationToken);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
