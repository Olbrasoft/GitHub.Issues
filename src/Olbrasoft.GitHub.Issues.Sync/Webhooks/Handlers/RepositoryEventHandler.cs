using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

/// <summary>
/// Handler for GitHub repository webhook events.
/// Handles: created (auto-discovery of new repositories)
/// </summary>
public class RepositoryEventHandler : IWebhookEventHandler<GitHubRepositoryWebhookPayload>
{
    private readonly IRepositorySyncBusinessService _repositoryService;
    private readonly ILogger<RepositoryEventHandler> _logger;

    public RepositoryEventHandler(
        IRepositorySyncBusinessService repositoryService,
        ILogger<RepositoryEventHandler> logger)
    {
        _repositoryService = repositoryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookProcessingResult> HandleAsync(
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
}
