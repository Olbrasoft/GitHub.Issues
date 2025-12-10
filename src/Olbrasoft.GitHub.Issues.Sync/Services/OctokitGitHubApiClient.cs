using Microsoft.Extensions.Options;
using Octokit;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Implementation of IGitHubApiClient using Octokit library.
/// </summary>
public class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly GitHubClient _client;

    public OctokitGitHubApiClient(IOptions<GitHubSettings> settings)
    {
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
}
