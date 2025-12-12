using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

/// <summary>
/// Handler for GitHub issue comment webhook events.
/// Regenerates embeddings when comments are added/edited/deleted.
/// </summary>
public class IssueCommentEventHandler : IWebhookEventHandler<GitHubIssueCommentWebhookPayload>
{
    private readonly IRepositorySyncBusinessService _repositoryService;
    private readonly IIssueSyncBusinessService _issueService;
    private readonly IIssueEmbeddingGenerator _embeddingGenerator;
    private readonly IIssueUpdateNotifier _updateNotifier;
    private readonly ILogger<IssueCommentEventHandler> _logger;

    public IssueCommentEventHandler(
        IRepositorySyncBusinessService repositoryService,
        IIssueSyncBusinessService issueService,
        IIssueEmbeddingGenerator embeddingGenerator,
        IIssueUpdateNotifier updateNotifier,
        ILogger<IssueCommentEventHandler> logger)
    {
        _repositoryService = repositoryService;
        _issueService = issueService;
        _embeddingGenerator = embeddingGenerator;
        _updateNotifier = updateNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> HandleAsync(
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

            var existingIssue = await _issueService.GetIssueAsync(repository.Id, issue.Number, ct);
            if (existingIssue == null)
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

            // Regenerate embedding with updated comments
            var (owner, repoName) = ParseRepoFullName(repo.FullName);
            var labelNames = issue.Labels.Select(l => l.Name).ToList();
            var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
                owner, repoName, issue.Number, issue.Title, issue.Body, labelNames, ct);
            if (embedding == null)
            {
                _logger.LogError("Failed to generate embedding for issue #{Number}", issue.Number);
                return new WebhookProcessingResult
                {
                    Success = false,
                    Message = "Embedding generation failed - will retry",
                    IssueNumber = issue.Number,
                    RepositoryFullName = repo.FullName,
                    EmbeddingGenerated = false
                };
            }

            var savedIssue = await _issueService.SaveIssueAsync(
                repository.Id,
                issue.Number,
                issue.Title,
                issue.State.Equals("open", StringComparison.OrdinalIgnoreCase),
                issue.HtmlUrl,
                issue.UpdatedAt,
                DateTimeOffset.UtcNow,
                embedding,
                ct);

            await NotifyIssueUpdateAsync(savedIssue.Id, issue, ct);

            return new WebhookProcessingResult
            {
                Success = true,
                Message = $"Embedding regenerated (comment {action})",
                IssueNumber = issue.Number,
                RepositoryFullName = repo.FullName,
                EmbeddingGenerated = true
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
}
