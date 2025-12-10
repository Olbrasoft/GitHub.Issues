using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing repository information from GitHub.
/// </summary>
public class RepositorySyncService : IRepositorySyncService
{
    private readonly IRepositorySyncBusinessService _repositorySyncBusiness;
    private readonly IGitHubApiClient _gitHubApiClient;
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly SyncSettings _syncSettings;
    private readonly ILogger<RepositorySyncService> _logger;

    public RepositorySyncService(
        IRepositorySyncBusinessService repositorySyncBusiness,
        IGitHubApiClient gitHubApiClient,
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        IOptions<SyncSettings> syncSettings,
        ILogger<RepositorySyncService> logger)
    {
        _repositorySyncBusiness = repositorySyncBusiness;
        _gitHubApiClient = gitHubApiClient;
        _httpClient = httpClient;
        _settings = settings.Value;
        _syncSettings = syncSettings.Value;
        _logger = logger;

        // Configure HttpClient for GitHub API
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }

    public async Task<Repository> EnsureRepositoryAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var fullName = $"{owner}/{repo}";
        var repository = await _repositorySyncBusiness.GetByFullNameAsync(fullName, cancellationToken);

        if (repository == null)
        {
            var ghRepo = await _gitHubApiClient.GetRepositoryAsync(owner, repo);

            repository = await _repositorySyncBusiness.SaveRepositoryAsync(
                ghRepo.Id,
                ghRepo.FullName,
                ghRepo.HtmlUrl,
                cancellationToken);

            _logger.LogInformation("Created repository: {FullName}", fullName);
        }

        return repository;
    }

    public async Task<List<string>> FetchAllRepositoriesForOwnerAsync(CancellationToken cancellationToken = default)
    {
        var repositories = new List<string>();
        var page = 1;
        var owner = _settings.Owner!;

        while (true)
        {
            // Use different API endpoint for users vs organizations
            var url = _settings.OwnerType.Equals("org", StringComparison.OrdinalIgnoreCase)
                ? $"orgs/{owner}/repos?per_page={_syncSettings.GitHubApiPageSize}&page={page}"
                : $"users/{owner}/repos?per_page={_syncSettings.GitHubApiPageSize}&type=all&page={page}";

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

                // Check has_issues - skip repos with issues disabled
                if (repoElement.TryGetProperty("has_issues", out var hasIssuesElement) && !hasIssuesElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: has_issues=false", fullName);
                    continue;
                }

                // Check archived - skip unless IncludeArchived is true
                if (!_settings.IncludeArchived &&
                    repoElement.TryGetProperty("archived", out var archivedElement) && archivedElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: archived", fullName);
                    continue;
                }

                // Check fork - skip unless IncludeForks is true
                if (!_settings.IncludeForks &&
                    repoElement.TryGetProperty("fork", out var forkElement) && forkElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: fork", fullName);
                    continue;
                }

                repositories.Add(fullName);
            }

            if (pageRepos.Count < _syncSettings.GitHubApiPageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Discovered {Count} repositories for {Owner}", repositories.Count, owner);
        return repositories;
    }
}
