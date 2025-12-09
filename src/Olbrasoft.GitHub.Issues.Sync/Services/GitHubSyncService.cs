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
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        GitHubDbContext dbContext,
        IEmbeddingService embeddingService,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _settings = settings.Value;
        _logger = logger;

        _gitHubClient = new GitHubClient(new ProductHeaderValue("Olbrasoft-GitHub-Issues-Sync"));

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _gitHubClient.Credentials = new Credentials(_settings.Token);
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

        // Generate embedding for new issues or if title changed
        if (isNew || issue.TitleEmbedding == null)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(ghIssue.Title, cancellationToken);
            if (embedding != null)
            {
                issue.TitleEmbedding = embedding;
            }
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
            var label = await _dbContext.Labels.FirstOrDefaultAsync(l => l.Name == labelName, cancellationToken);
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
                .FirstOrDefaultAsync(l => l.Name == ghLabel.Name, cancellationToken);

            if (label == null)
            {
                label = new Data.Entities.Label { Name = ghLabel.Name, Color = ghLabel.Color };
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
}
