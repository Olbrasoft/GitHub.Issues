using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

/// <summary>
/// Handler for GitHub label webhook events.
/// Handles: created, edited, deleted
/// </summary>
public class LabelEventHandler : IWebhookEventHandler<GitHubLabelWebhookPayload>
{
    private readonly IRepositorySyncBusinessService _repositoryService;
    private readonly ILabelSyncBusinessService _labelService;
    private readonly ILogger<LabelEventHandler> _logger;

    public LabelEventHandler(
        IRepositorySyncBusinessService repositoryService,
        ILabelSyncBusinessService labelService,
        ILogger<LabelEventHandler> logger)
    {
        _repositoryService = repositoryService;
        _labelService = labelService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> HandleAsync(
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
                "created" => await HandleCreatedAsync(repository.Id, label, repo.FullName, ct),
                "edited" => await HandleEditedAsync(repository.Id, label, payload.Changes, repo.FullName, ct),
                "deleted" => await HandleDeletedAsync(repository.Id, label, repo.FullName, ct),
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

    private async Task<WebhookProcessingResult> HandleCreatedAsync(
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

    private async Task<WebhookProcessingResult> HandleEditedAsync(
        int repositoryId,
        GitHubWebhookLabel label,
        GitHubLabelChanges? changes,
        string repoFullName,
        CancellationToken ct)
    {
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

    private async Task<WebhookProcessingResult> HandleDeletedAsync(
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
