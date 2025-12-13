using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

/// <summary>
/// Handler for GitHub issue webhook events.
/// Handles: opened, edited, closed, reopened, deleted, labeled, unlabeled
/// </summary>
public class IssueEventHandler : IWebhookEventHandler<GitHubIssueWebhookPayload>
{
    private readonly IRepositorySyncBusinessService _repositoryService;
    private readonly IIssueSyncBusinessService _issueService;
    private readonly IIssueEmbeddingGenerator _embeddingGenerator;
    private readonly IIssueUpdateNotifier _updateNotifier;
    private readonly ISearchResultNotifier _searchResultNotifier;
    private readonly ILogger<IssueEventHandler> _logger;

    public IssueEventHandler(
        IRepositorySyncBusinessService repositoryService,
        IIssueSyncBusinessService issueService,
        IIssueEmbeddingGenerator embeddingGenerator,
        IIssueUpdateNotifier updateNotifier,
        ISearchResultNotifier searchResultNotifier,
        ILogger<IssueEventHandler> logger)
    {
        _repositoryService = repositoryService;
        _issueService = issueService;
        _embeddingGenerator = embeddingGenerator;
        _updateNotifier = updateNotifier;
        _searchResultNotifier = searchResultNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> HandleAsync(
        GitHubIssueWebhookPayload payload,
        CancellationToken ct = default)
    {
        var action = payload.Action;
        var issue = payload.Issue;
        var repo = payload.Repository;

        _logger.LogInformation(
            "Processing webhook: {Action} for issue #{Number} in {Repo}",
            action, issue.Number, repo.FullName);

        // Skip pull requests
        if (issue.IsPullRequest)
        {
            _logger.LogDebug("Skipping pull request #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = true,
                Message = "Skipped (pull request)",
                IssueNumber = issue.Number,
                RepositoryFullName = repo.FullName
            };
        }

        // Get or create repository
        var repository = await _repositoryService.GetByFullNameAsync(repo.FullName, ct);
        if (repository == null)
        {
            repository = await _repositoryService.SaveRepositoryAsync(
                repo.Id,
                repo.FullName,
                repo.HtmlUrl,
                ct);
            _logger.LogInformation("Created new repository: {Repo}", repo.FullName);
        }

        try
        {
            return action.ToLowerInvariant() switch
            {
                "opened" => await HandleOpenedAsync(repository.Id, repo.FullName, issue, ct),
                "edited" => await HandleEditedAsync(repository.Id, repo.FullName, issue, ct),
                "closed" => await HandleStateChangeAsync(repository.Id, issue, isOpen: false, ct),
                "reopened" => await HandleStateChangeAsync(repository.Id, issue, isOpen: true, ct),
                "deleted" => await HandleDeletedAsync(repository.Id, issue, ct),
                "labeled" or "unlabeled" => await HandleLabelChangeAsync(repository.Id, repo.FullName, issue, ct),
                _ => new WebhookProcessingResult
                {
                    Success = true,
                    Message = $"Ignored action: {action}",
                    IssueNumber = issue.Number,
                    RepositoryFullName = repo.FullName
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook for issue #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                IssueNumber = issue.Number,
                RepositoryFullName = repo.FullName
            };
        }
    }

    private async Task<WebhookProcessingResult> HandleOpenedAsync(
        int repositoryId,
        string repoFullName,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating new issue #{Number}: {Title}", issue.Number, issue.Title);

        var (owner, repo) = ParseRepoFullName(repoFullName);
        var labelNames = issue.Labels.Select(l => l.Name).ToList();

        var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
            owner, repo, issue.Number, issue.Title, issue.Body, labelNames, ct);
        if (embedding == null)
        {
            _logger.LogError("Failed to generate embedding for issue #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = "Embedding generation failed - will retry",
                IssueNumber = issue.Number,
                RepositoryFullName = repoFullName,
                EmbeddingGenerated = false
            };
        }

        var savedIssue = await _issueService.SaveIssueAsync(
            repositoryId,
            issue.Number,
            issue.Title,
            issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
            issue.HtmlUrl,
            issue.UpdatedAt,
            DateTimeOffset.UtcNow,
            embedding,
            ct);

        if (labelNames.Count > 0)
        {
            await _issueService.SyncLabelsAsync(savedIssue.Id, repositoryId, labelNames, ct);
        }

        await NotifyIssueUpdateAsync(savedIssue.Id, issue, ct);

        // Notify search result subscribers about the new issue
        await NotifyNewIssueAsync(savedIssue.Id, repoFullName, issue, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Issue created",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            RepositoryFullName = repoFullName,
            EmbeddingGenerated = true
        };
    }

    private async Task<WebhookProcessingResult> HandleEditedAsync(
        int repositoryId,
        string repoFullName,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating issue #{Number}: {Title}", issue.Number, issue.Title);

        var (owner, repo) = ParseRepoFullName(repoFullName);
        var labelNames = issue.Labels.Select(l => l.Name).ToList();

        var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
            owner, repo, issue.Number, issue.Title, issue.Body, labelNames, ct);
        if (embedding == null)
        {
            _logger.LogError("Failed to generate embedding for issue #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = "Embedding generation failed - will retry",
                IssueNumber = issue.Number,
                RepositoryFullName = repoFullName,
                EmbeddingGenerated = false
            };
        }

        var savedIssue = await _issueService.SaveIssueAsync(
            repositoryId,
            issue.Number,
            issue.Title,
            issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
            issue.HtmlUrl,
            issue.UpdatedAt,
            DateTimeOffset.UtcNow,
            embedding,
            ct);

        await _issueService.SyncLabelsAsync(savedIssue.Id, repositoryId, labelNames, ct);
        await NotifyIssueUpdateAsync(savedIssue.Id, issue, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Issue updated",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            RepositoryFullName = repoFullName,
            EmbeddingGenerated = true
        };
    }

    private async Task<WebhookProcessingResult> HandleStateChangeAsync(
        int repositoryId,
        GitHubWebhookIssue issue,
        bool isOpen,
        CancellationToken ct)
    {
        var stateText = isOpen ? "reopened" : "closed";
        _logger.LogInformation("Issue #{Number} {State}", issue.Number, stateText);

        var existingIssue = await _issueService.GetIssueAsync(repositoryId, issue.Number, ct);
        if (existingIssue?.Embedding == null || existingIssue.Embedding.Length == 0)
        {
            _logger.LogError("Issue #{Number} has no embedding", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = "Issue has no embedding - full sync required",
                IssueNumber = issue.Number,
                EmbeddingGenerated = false
            };
        }

        var savedIssue = await _issueService.SaveIssueAsync(
            repositoryId,
            issue.Number,
            issue.Title,
            isOpen,
            issue.HtmlUrl,
            issue.UpdatedAt,
            DateTimeOffset.UtcNow,
            existingIssue.Embedding,
            ct);

        await NotifyIssueUpdateAsync(savedIssue.Id, issue, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = $"Issue {stateText}",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            EmbeddingGenerated = false
        };
    }

    private async Task<WebhookProcessingResult> HandleDeletedAsync(
        int repositoryId,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing deletion of issue #{Number}", issue.Number);

        // Get existing issue to find its ID
        var existingIssue = await _issueService.GetIssueAsync(repositoryId, issue.Number, ct);
        if (existingIssue == null)
        {
            _logger.LogDebug("Issue #{Number} not found in database - nothing to delete", issue.Number);
            return new WebhookProcessingResult
            {
                Success = true,
                Message = "Issue not found in database (already deleted or never synced)",
                IssueNumber = issue.Number,
                EmbeddingGenerated = false
            };
        }

        // Soft delete the issue
        var deletedCount = await _issueService.MarkIssuesAsDeletedAsync(
            repositoryId,
            [existingIssue.Id],
            ct);

        _logger.LogInformation(
            "Issue #{Number} ({Title}) marked as deleted via webhook",
            issue.Number, existingIssue.Title);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = deletedCount > 0 ? "Issue deleted" : "Issue was already deleted",
            IssueTitle = existingIssue.Title,
            IssueNumber = issue.Number,
            EmbeddingGenerated = false
        };
    }

    private async Task<WebhookProcessingResult> HandleLabelChangeAsync(
        int repositoryId,
        string repoFullName,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating labels for issue #{Number}", issue.Number);

        var existingIssue = await _issueService.GetIssueAsync(repositoryId, issue.Number, ct);
        if (existingIssue == null)
        {
            return await HandleOpenedAsync(repositoryId, repoFullName, issue, ct);
        }

        var (owner, repo) = ParseRepoFullName(repoFullName);
        var labelNames = issue.Labels.Select(l => l.Name).ToList();
        var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
            owner, repo, issue.Number, issue.Title, issue.Body, labelNames, ct);
        if (embedding == null)
        {
            _logger.LogError("Failed to generate embedding for issue #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = "Embedding generation failed - will retry",
                IssueNumber = issue.Number,
                RepositoryFullName = repoFullName,
                EmbeddingGenerated = false
            };
        }

        var savedIssue = await _issueService.SaveIssueAsync(
            repositoryId,
            issue.Number,
            issue.Title,
            issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
            issue.HtmlUrl,
            issue.UpdatedAt,
            DateTimeOffset.UtcNow,
            embedding,
            ct);

        await _issueService.SyncLabelsAsync(savedIssue.Id, repositoryId, labelNames, ct);
        await NotifyIssueUpdateAsync(savedIssue.Id, issue, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Labels updated (embedding regenerated)",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            RepositoryFullName = repoFullName,
            EmbeddingGenerated = true
        };
    }

    private static (string Owner, string Repo) ParseRepoFullName(string fullName)
    {
        var parts = fullName.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid repository full name format: {fullName}", nameof(fullName));
        }
        return (parts[0], parts[1]);
    }

    private async Task NotifyIssueUpdateAsync(int issueId, GitHubWebhookIssue issue, CancellationToken ct)
    {
        try
        {
            var update = new IssueUpdateDto(
                IssueId: issueId,
                GitHubNumber: issue.Number,
                IsOpen: issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
                Title: issue.Title,
                Labels: issue.Labels.Select(l => new LabelDto(l.Name, l.Color)).ToList());

            await _updateNotifier.NotifyIssueUpdatedAsync(update, ct);
            _logger.LogDebug("Notified clients about update to issue #{Number}", issue.Number);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify clients about issue #{Number} update", issue.Number);
        }
    }

    private async Task NotifyNewIssueAsync(int issueId, string repoFullName, GitHubWebhookIssue issue, CancellationToken ct)
    {
        try
        {
            var newIssue = new NewIssueDto(
                IssueId: issueId,
                GitHubNumber: issue.Number,
                RepositoryFullName: repoFullName,
                IsOpen: issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
                Title: issue.Title,
                Url: issue.HtmlUrl,
                Labels: issue.Labels.Select(l => new LabelDto(l.Name, l.Color)).ToList());

            await _searchResultNotifier.NotifyNewIssueAsync(newIssue, ct);
            _logger.LogInformation("Notified search result subscribers about new issue #{Number} in {Repo}",
                issue.Number, repoFullName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify search result subscribers about new issue #{Number}", issue.Number);
        }
    }
}
