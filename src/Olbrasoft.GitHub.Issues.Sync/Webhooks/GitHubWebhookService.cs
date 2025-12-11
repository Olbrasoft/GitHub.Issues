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
        IEmbeddingService embeddingService,
        IEmbeddingTextBuilder textBuilder,
        ILogger<GitHubWebhookService> logger)
    {
        _repositoryService = repositoryService;
        _issueService = issueService;
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
}
