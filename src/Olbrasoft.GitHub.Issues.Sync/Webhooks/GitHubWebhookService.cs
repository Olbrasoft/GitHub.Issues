using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Service for processing GitHub webhook events.
/// Handles issue lifecycle events and generates embeddings for new/changed issues.
/// </summary>
public class GitHubWebhookService : IGitHubWebhookService
{
    private readonly IRepositorySyncBusinessService _repositoryService;
    private readonly IIssueSyncBusinessService _issueService;
    private readonly ILabelSyncBusinessService _labelService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEmbeddingTextBuilder _textBuilder;
    private readonly ILogger<GitHubWebhookService> _logger;

    // Actions that require embedding regeneration
    private static readonly HashSet<string> EmbeddingRequiredActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "opened",
        "edited"
    };

    public GitHubWebhookService(
        IRepositorySyncBusinessService repositoryService,
        IIssueSyncBusinessService issueService,
        ILabelSyncBusinessService labelService,
        IEmbeddingService embeddingService,
        IEmbeddingTextBuilder textBuilder,
        ILogger<GitHubWebhookService> logger)
    {
        _repositoryService = repositoryService;
        _issueService = issueService;
        _labelService = labelService;
        _embeddingService = embeddingService;
        _textBuilder = textBuilder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> ProcessIssueEventAsync(
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
            // Repository not in our database - create it
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
                "opened" => await HandleOpenedAsync(repository.Id, issue, ct),
                "edited" => await HandleEditedAsync(repository.Id, issue, ct),
                "closed" => await HandleStateChangeAsync(repository.Id, issue, isOpen: false, ct),
                "reopened" => await HandleStateChangeAsync(repository.Id, issue, isOpen: true, ct),
                "deleted" => await HandleDeletedAsync(repository.Id, issue, ct),
                "labeled" or "unlabeled" => await HandleLabelChangeAsync(repository.Id, issue, ct),
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
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating new issue #{Number}: {Title}", issue.Number, issue.Title);

        // Generate embedding for new issue
        var embedding = await GenerateEmbeddingAsync(issue.Title, issue.Body, ct);

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

        // Sync labels
        var labelNames = issue.Labels.Select(l => l.Name).ToList();
        if (labelNames.Count > 0)
        {
            await _issueService.SyncLabelsAsync(savedIssue.Id, repositoryId, labelNames, ct);
        }

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Issue created",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            EmbeddingGenerated = embedding != null
        };
    }

    private async Task<WebhookProcessingResult> HandleEditedAsync(
        int repositoryId,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating issue #{Number}: {Title}", issue.Number, issue.Title);

        // Generate new embedding for edited issue (title/body may have changed)
        var embedding = await GenerateEmbeddingAsync(issue.Title, issue.Body, ct);

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

        // Sync labels
        var labelNames = issue.Labels.Select(l => l.Name).ToList();
        await _issueService.SyncLabelsAsync(savedIssue.Id, repositoryId, labelNames, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Issue updated",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            EmbeddingGenerated = embedding != null
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

        // Get existing issue to preserve embedding
        var existingIssue = await _issueService.GetIssueAsync(repositoryId, issue.Number, ct);

        await _issueService.SaveIssueAsync(
            repositoryId,
            issue.Number,
            issue.Title,
            isOpen,
            issue.HtmlUrl,
            issue.UpdatedAt,
            DateTimeOffset.UtcNow,
            existingIssue?.Embedding, // Preserve existing embedding
            ct);

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
        _logger.LogInformation("Deleting issue #{Number}", issue.Number);

        // Note: We don't actually delete issues from our database
        // Instead, we could mark them as deleted or update state
        // For now, log and skip - batch sync will reconcile
        _logger.LogWarning(
            "Issue deletion not implemented - issue #{Number} will be removed on next full sync",
            issue.Number);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Issue deletion logged (will be removed on next sync)",
            IssueNumber = issue.Number,
            EmbeddingGenerated = false
        };
    }

    private async Task<WebhookProcessingResult> HandleLabelChangeAsync(
        int repositoryId,
        GitHubWebhookIssue issue,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating labels for issue #{Number}", issue.Number);

        // Get or create the issue first
        var existingIssue = await _issueService.GetIssueAsync(repositoryId, issue.Number, ct);

        if (existingIssue == null)
        {
            // Issue not in our database yet - create it with embedding
            return await HandleOpenedAsync(repositoryId, issue, ct);
        }

        // Just sync labels, no need to regenerate embedding
        var labelNames = issue.Labels.Select(l => l.Name).ToList();
        await _issueService.SyncLabelsAsync(existingIssue.Id, repositoryId, labelNames, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = "Labels updated",
            IssueTitle = issue.Title,
            IssueNumber = issue.Number,
            EmbeddingGenerated = false
        };
    }

    private async Task<float[]?> GenerateEmbeddingAsync(string title, string? body, CancellationToken ct)
    {
        try
        {
            var text = _textBuilder.CreateEmbeddingText(title, body);
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                text,
                EmbeddingInputType.Document,
                ct);

            if (embedding == null)
            {
                _logger.LogWarning("Failed to generate embedding for issue");
            }

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return null;
        }
    }

    // ===== Issue Comment Event Handler =====

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> ProcessIssueCommentEventAsync(
        GitHubIssueCommentWebhookPayload payload,
        CancellationToken ct = default)
    {
        var action = payload.Action;
        var issue = payload.Issue;
        var repo = payload.Repository;

        _logger.LogInformation(
            "Processing issue_comment webhook: {Action} for issue #{Number} in {Repo}",
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

        try
        {
            // Get repository
            var repository = await _repositoryService.GetByFullNameAsync(repo.FullName, ct);
            if (repository == null)
            {
                _logger.LogWarning("Repository {Repo} not found in database", repo.FullName);
                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = "Repository not synced",
                    IssueNumber = issue.Number,
                    RepositoryFullName = repo.FullName
                };
            }

            // Update comment count
            var updated = await _issueService.UpdateCommentCountAsync(
                repository.Id,
                issue.Number,
                issue.Comments,
                ct);

            if (!updated)
            {
                _logger.LogWarning("Issue #{Number} not found in database", issue.Number);
                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = "Issue not synced",
                    IssueNumber = issue.Number,
                    RepositoryFullName = repo.FullName
                };
            }

            return new WebhookProcessingResult
            {
                Success = true,
                Message = $"Comment count updated to {issue.Comments}",
                IssueNumber = issue.Number,
                RepositoryFullName = repo.FullName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing issue_comment webhook for issue #{Number}", issue.Number);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                IssueNumber = issue.Number,
                RepositoryFullName = repo.FullName
            };
        }
    }

    // ===== Repository Event Handler =====

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> ProcessRepositoryEventAsync(
        GitHubRepositoryWebhookPayload payload,
        CancellationToken ct = default)
    {
        var action = payload.Action;
        var repo = payload.Repository;

        _logger.LogInformation(
            "Processing repository webhook: {Action} for {Repo}",
            action, repo.FullName);

        try
        {
            if (!action.Equals("created", StringComparison.OrdinalIgnoreCase))
            {
                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = $"Ignored action: {action}",
                    RepositoryFullName = repo.FullName
                };
            }

            // Check if repository already exists
            var existing = await _repositoryService.GetByFullNameAsync(repo.FullName, ct);
            if (existing != null)
            {
                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = "Repository already exists",
                    RepositoryFullName = repo.FullName
                };
            }

            // Auto-add new repository
            await _repositoryService.SaveRepositoryAsync(
                repo.Id,
                repo.FullName,
                repo.HtmlUrl,
                ct);

            _logger.LogInformation("Auto-discovered new repository: {Repo}", repo.FullName);

            return new WebhookProcessingResult
            {
                Success = true,
                Message = "Repository auto-discovered and added",
                RepositoryFullName = repo.FullName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing repository webhook for {Repo}", repo.FullName);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RepositoryFullName = repo.FullName
            };
        }
    }

    // ===== Label Event Handler =====

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> ProcessLabelEventAsync(
        GitHubLabelWebhookPayload payload,
        CancellationToken ct = default)
    {
        var action = payload.Action;
        var label = payload.Label;
        var repo = payload.Repository;

        _logger.LogInformation(
            "Processing label webhook: {Action} for label '{Label}' in {Repo}",
            action, label.Name, repo.FullName);

        try
        {
            // Get repository
            var repository = await _repositoryService.GetByFullNameAsync(repo.FullName, ct);
            if (repository == null)
            {
                _logger.LogWarning("Repository {Repo} not found in database", repo.FullName);
                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = "Repository not synced",
                    RepositoryFullName = repo.FullName
                };
            }

            return action.ToLowerInvariant() switch
            {
                "created" => await HandleLabelCreatedAsync(repository.Id, label, repo.FullName, ct),
                "edited" => await HandleLabelEditedAsync(repository.Id, label, payload.Changes, repo.FullName, ct),
                "deleted" => await HandleLabelDeletedAsync(repository.Id, label, repo.FullName, ct),
                _ => new WebhookProcessingResult
                {
                    Success = true,
                    Message = $"Ignored action: {action}",
                    RepositoryFullName = repo.FullName
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing label webhook for '{Label}'", label.Name);
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                RepositoryFullName = repo.FullName
            };
        }
    }

    private async Task<WebhookProcessingResult> HandleLabelCreatedAsync(
        int repositoryId,
        GitHubWebhookLabel label,
        string repoFullName,
        CancellationToken ct)
    {
        await _labelService.SaveLabelAsync(repositoryId, label.Name, label.Color, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = $"Label '{label.Name}' created",
            RepositoryFullName = repoFullName
        };
    }

    private async Task<WebhookProcessingResult> HandleLabelEditedAsync(
        int repositoryId,
        GitHubWebhookLabel label,
        GitHubLabelChanges? changes,
        string repoFullName,
        CancellationToken ct)
    {
        // If name changed, we need to delete old and create new
        if (changes?.Name != null)
        {
            await _labelService.DeleteLabelAsync(repositoryId, changes.Name.From, ct);
        }

        await _labelService.SaveLabelAsync(repositoryId, label.Name, label.Color, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = $"Label '{label.Name}' updated",
            RepositoryFullName = repoFullName
        };
    }

    private async Task<WebhookProcessingResult> HandleLabelDeletedAsync(
        int repositoryId,
        GitHubWebhookLabel label,
        string repoFullName,
        CancellationToken ct)
    {
        await _labelService.DeleteLabelAsync(repositoryId, label.Name, ct);

        return new WebhookProcessingResult
        {
            Success = true,
            Message = $"Label '{label.Name}' deleted",
            RepositoryFullName = repoFullName
        };
    }
}
