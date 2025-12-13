using Microsoft.AspNetCore.SignalR;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

/// <summary>
/// SignalR implementation of ISearchResultNotifier.
/// Broadcasts new issue notifications to clients viewing search results.
/// </summary>
public class SignalRSearchResultNotifier : ISearchResultNotifier
{
    private readonly IHubContext<IssueUpdatesHub> _hubContext;
    private readonly ILogger<SignalRSearchResultNotifier> _logger;

    /// <summary>
    /// Group name for clients subscribed to search result updates.
    /// </summary>
    public const string SearchResultsGroup = "search-results";

    public SignalRSearchResultNotifier(
        IHubContext<IssueUpdatesHub> hubContext,
        ILogger<SignalRSearchResultNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyNewIssueAsync(NewIssueDto newIssue, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(SearchResultsGroup).SendAsync(
            "NewIssueAdded",
            new
            {
                IssueId = newIssue.IssueId,
                GitHubNumber = newIssue.GitHubNumber,
                RepositoryFullName = newIssue.RepositoryFullName,
                IsOpen = newIssue.IsOpen,
                Title = newIssue.Title,
                Url = newIssue.Url,
                Labels = newIssue.Labels.Select(l => new { l.Name, l.Color })
            },
            cancellationToken);

        _logger.LogInformation(
            "Broadcast new issue #{Number} ({Title}) from {Repo} to search result subscribers",
            newIssue.GitHubNumber, newIssue.Title, newIssue.RepositoryFullName);
    }
}
