using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

public class GitHubSyncService : IGitHubSyncService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly GitHubClient _gitHubClient;
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        GitHubDbContext dbContext,
        IEmbeddingService embeddingService,
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _gitHubClient = new GitHubClient(new Octokit.ProductHeaderValue("Olbrasoft-GitHub-Issues-Sync"));

        // Configure HttpClient for GitHub API
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _gitHubClient.Credentials = new Credentials(_settings.Token);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }

    public async Task SyncAllRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var repoFullName in _settings.Repositories)
        {
            var parts = repoFullName.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid repository format: {Repository}. Expected 'owner/repo'", repoFullName);
                continue;
            }

            await SyncRepositoryAsync(parts[0], parts[1], cancellationToken);
        }
    }

    public async Task SyncRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync for {Owner}/{Repo}", owner, repo);

        var repository = await EnsureRepositoryAsync(owner, repo, cancellationToken);
        await SyncLabelsAsync(repository, owner, repo, cancellationToken);
        await SyncIssuesAsync(repository, owner, repo, cancellationToken);
        await SyncSubIssuesAsync(repository, owner, repo, cancellationToken);

        _logger.LogInformation("Completed sync for {Owner}/{Repo}", owner, repo);
    }

    private async Task<Data.Entities.Repository> EnsureRepositoryAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var fullName = $"{owner}/{repo}";
        var repository = await _dbContext.Repositories
            .FirstOrDefaultAsync(r => r.FullName == fullName, cancellationToken);

        if (repository == null)
        {
            var ghRepo = await _gitHubClient.Repository.Get(owner, repo);

            repository = new Data.Entities.Repository
            {
                GitHubId = ghRepo.Id,
                FullName = ghRepo.FullName,
                HtmlUrl = ghRepo.HtmlUrl
            };

            _dbContext.Repositories.Add(repository);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created repository: {FullName}", fullName);
        }

        return repository;
    }

    private async Task SyncIssuesAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var lastSyncedIssue = await _dbContext.Issues
            .Where(i => i.RepositoryId == repository.Id)
            .OrderByDescending(i => i.GitHubUpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var request = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            SortProperty = IssueSort.Updated,
            SortDirection = SortDirection.Descending
        };

        if (lastSyncedIssue != null)
        {
            request.Since = lastSyncedIssue.GitHubUpdatedAt;
        }

        var options = new ApiOptions { PageSize = 100 };
        var issues = await _gitHubClient.Issue.GetAllForRepository(owner, repo, request, options);

        _logger.LogInformation("Found {Count} issues to sync for {Owner}/{Repo}", issues.Count, owner, repo);

        var syncedAt = DateTimeOffset.UtcNow;

        foreach (var ghIssue in issues)
        {
            // Skip pull requests (GitHub API returns them as issues)
            if (ghIssue.PullRequest != null)
            {
                continue;
            }

            await SyncIssueAsync(repository, ghIssue, syncedAt, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncIssueAsync(
        Data.Entities.Repository repository,
        Octokit.Issue ghIssue,
        DateTimeOffset syncedAt,
        CancellationToken cancellationToken)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.IssueLabels)
            .FirstOrDefaultAsync(i => i.RepositoryId == repository.Id && i.Number == ghIssue.Number, cancellationToken);

        var isNew = issue == null;

        if (isNew)
        {
            issue = new Data.Entities.Issue
            {
                RepositoryId = repository.Id,
                Number = ghIssue.Number
            };
            _dbContext.Issues.Add(issue);
        }

        issue!.Title = ghIssue.Title;
        issue.IsOpen = ghIssue.State.Value == ItemState.Open;
        issue.Url = ghIssue.HtmlUrl;
        issue.GitHubUpdatedAt = ghIssue.UpdatedAt ?? ghIssue.CreatedAt;
        issue.SyncedAt = syncedAt;

        // Generate embedding for new issues
        if (isNew)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(ghIssue.Title, cancellationToken);
            if (embedding == null)
            {
                throw new InvalidOperationException($"Failed to generate embedding for issue #{ghIssue.Number}. Ollama may be unavailable.");
            }
            issue.TitleEmbedding = embedding;
        }

        // Sync labels
        var existingLabelIds = issue.IssueLabels.Select(il => il.LabelId).ToHashSet();
        var ghLabelNames = ghIssue.Labels.Select(l => l.Name).ToList();

        // Remove labels that are no longer present
        var labelsToRemove = issue.IssueLabels
            .Where(il => !ghLabelNames.Contains(_dbContext.Labels.Find(il.LabelId)?.Name ?? ""))
            .ToList();

        foreach (var labelToRemove in labelsToRemove)
        {
            issue.IssueLabels.Remove(labelToRemove);
        }

        // Add new labels
        foreach (var labelName in ghLabelNames)
        {
            var label = await _dbContext.Labels.FirstOrDefaultAsync(l => l.RepositoryId == repository.Id && l.Name == labelName, cancellationToken);
            if (label != null && !existingLabelIds.Contains(label.Id))
            {
                issue.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = label.Id });
            }
        }

        _logger.LogDebug("Synced issue #{Number}: {Title}", ghIssue.Number, ghIssue.Title);
    }

    private async Task SyncLabelsAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var ghLabels = await _gitHubClient.Issue.Labels.GetAllForRepository(owner, repo);

        foreach (var ghLabel in ghLabels)
        {
            var label = await _dbContext.Labels
                .FirstOrDefaultAsync(l => l.RepositoryId == repository.Id && l.Name == ghLabel.Name, cancellationToken);

            if (label == null)
            {
                label = new Data.Entities.Label { RepositoryId = repository.Id, Name = ghLabel.Name, Color = ghLabel.Color };
                _dbContext.Labels.Add(label);
                _logger.LogDebug("Created label: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
            else if (label.Color != ghLabel.Color)
            {
                label.Color = ghLabel.Color;
                _logger.LogDebug("Updated label color: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncSubIssuesAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing sub-issues hierarchy for {Owner}/{Repo}", owner, repo);

        // Get all issues for this repository
        var issues = await _dbContext.Issues
            .Where(i => i.RepositoryId == repository.Id)
            .ToListAsync(cancellationToken);

        var issuesByNumber = issues.ToDictionary(i => i.Number);
        var updatedCount = 0;

        foreach (var issue in issues)
        {
            try
            {
                var subIssueNumbers = await GetSubIssueNumbersAsync(owner, repo, issue.Number, cancellationToken);

                foreach (var subIssueNumber in subIssueNumbers)
                {
                    if (issuesByNumber.TryGetValue(subIssueNumber, out var childIssue))
                    {
                        if (childIssue.ParentIssueId != issue.Id)
                        {
                            childIssue.ParentIssueId = issue.Id;
                            updatedCount++;
                            _logger.LogDebug("Set parent of issue #{Child} to #{Parent}", subIssueNumber, issue.Number);
                        }
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Issue has no sub-issues endpoint (404) - this is normal for issues without sub-issues
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch sub-issues for issue #{Number}", issue.Number);
            }
        }

        if (updatedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated {Count} parent-child relationships", updatedCount);
        }
        else
        {
            _logger.LogInformation("No sub-issues relationships found");
        }
    }

    private async Task<List<int>> GetSubIssueNumbersAsync(
        string owner,
        string repo,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var result = new List<int>();
        var url = $"repos/{owner}/{repo}/issues/{issueNumber}/sub_issues?per_page=100";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return result; // No sub-issues
            }
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("number", out var numberProperty))
            {
                result.Add(numberProperty.GetInt32());
            }
        }

        return result;
    }
}
