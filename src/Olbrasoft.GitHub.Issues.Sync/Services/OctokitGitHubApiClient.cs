using Microsoft.Extensions.Options;
using Octokit;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Implementation of IGitHubApiClient using Octokit library.
/// </summary>
public class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly GitHubClient _client;

    public OctokitGitHubApiClient(IOptions<GitHubSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _client = new GitHubClient(new ProductHeaderValue("Olbrasoft-GitHub-Issues-Sync"));

        if (!string.IsNullOrEmpty(settings.Value.Token))
        {
            _client.Credentials = new Credentials(settings.Value.Token);
        }
    }

    public async Task<Repository> GetRepositoryAsync(string owner, string repo)
    {
        return await _client.Repository.Get(owner, repo);
    }

    public async Task<IReadOnlyList<Label>> GetLabelsForRepositoryAsync(string owner, string repo)
    {
        return await _client.Issue.Labels.GetAllForRepository(owner, repo);
    }

    public async Task<Issue> UpdateIssueStateAsync(string owner, string repo, int issueNumber, string state)
    {
        var issueState = state.ToLowerInvariant() switch
        {
            "open" => ItemState.Open,
            "closed" => ItemState.Closed,
            _ => throw new ArgumentException($"Invalid state: {state}. Must be 'open' or 'closed'.", nameof(state))
        };

        var update = new IssueUpdate { State = issueState };
        return await _client.Issue.Update(owner, repo, issueNumber, update);
    }
}
