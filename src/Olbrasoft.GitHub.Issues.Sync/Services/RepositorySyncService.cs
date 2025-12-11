using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Orchestrates repository synchronization from GitHub.
/// Single responsibility: Coordinate sync workflow between API client and business services.
/// </summary>
public class RepositorySyncService : IRepositorySyncService
{
    private readonly IGitHubRepositoryApiClient _apiClient;
    private readonly IRepositorySyncBusinessService _repositorySyncBusiness;
    private readonly IGitHubApiClient _gitHubApiClient;
    private readonly GitHubSettings _settings;
    private readonly ILogger<RepositorySyncService> _logger;

    public RepositorySyncService(
        IGitHubRepositoryApiClient apiClient,
        IRepositorySyncBusinessService repositorySyncBusiness,
        IGitHubApiClient gitHubApiClient,
        IOptions<GitHubSettings> settings,
        ILogger<RepositorySyncService> logger)
    {
        _apiClient = apiClient;
        _repositorySyncBusiness = repositorySyncBusiness;
        _gitHubApiClient = gitHubApiClient;
        _settings = settings.Value;
        _logger = logger;
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
        var owner = _settings.Owner!;
        var repositories = await _apiClient.FetchRepositoriesForOwnerAsync(
            owner,
            _settings.OwnerType,
            _settings.IncludeArchived,
            _settings.IncludeForks,
            cancellationToken);

        return repositories.ToList();
    }
}
