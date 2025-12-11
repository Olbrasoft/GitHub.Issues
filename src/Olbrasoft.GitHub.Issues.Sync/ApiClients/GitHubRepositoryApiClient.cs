using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// GitHub REST API client for fetching repositories.
/// Single responsibility: HTTP communication and JSON parsing for repositories.
/// </summary>
public class GitHubRepositoryApiClient : IGitHubRepositoryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly int _pageSize;
    private readonly ILogger<GitHubRepositoryApiClient> _logger;

    public GitHubRepositoryApiClient(
        HttpClient httpClient,
        IOptions<SyncSettings> syncSettings,
        ILogger<GitHubRepositoryApiClient> logger)
    {
        _httpClient = httpClient;
        _pageSize = syncSettings.Value.GitHubApiPageSize;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> FetchRepositoriesForOwnerAsync(
        string owner,
        string ownerType,
        bool includeArchived,
        bool includeForks,
        CancellationToken cancellationToken = default)
    {
        var repositories = new List<string>();
        var page = 1;

        while (true)
        {
            var url = ownerType.Equals("org", StringComparison.OrdinalIgnoreCase)
                ? $"orgs/{owner}/repos?per_page={_pageSize}&page={page}"
                : $"users/{owner}/repos?per_page={_pageSize}&type=all&page={page}";

            _logger.LogDebug("Fetching repositories page {Page} for {Owner}", page, owner);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageRepos = doc.RootElement.EnumerateArray().ToList();
            if (pageRepos.Count == 0)
            {
                break;
            }

            foreach (var repoElement in pageRepos)
            {
                var fullName = repoElement.GetProperty("full_name").GetString();
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                // Skip repos with issues disabled
                if (repoElement.TryGetProperty("has_issues", out var hasIssuesElement) && !hasIssuesElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: has_issues=false", fullName);
                    continue;
                }

                // Skip archived unless included
                if (!includeArchived &&
                    repoElement.TryGetProperty("archived", out var archivedElement) && archivedElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: archived", fullName);
                    continue;
                }

                // Skip forks unless included
                if (!includeForks &&
                    repoElement.TryGetProperty("fork", out var forkElement) && forkElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: fork", fullName);
                    continue;
                }

                repositories.Add(fullName);
            }

            if (pageRepos.Count < _pageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Discovered {Count} repositories for {Owner}", repositories.Count, owner);
        return repositories;
    }
}
