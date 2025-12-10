using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing labels from GitHub.
/// </summary>
public class LabelSyncService : ILabelSyncService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubApiClient _gitHubApiClient;
    private readonly ILogger<LabelSyncService> _logger;

    public LabelSyncService(
        GitHubDbContext dbContext,
        IGitHubApiClient gitHubApiClient,
        ILogger<LabelSyncService> logger)
    {
        _dbContext = dbContext;
        _gitHubApiClient = gitHubApiClient;
        _logger = logger;
    }

    public async Task SyncLabelsAsync(
        Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var ghLabels = await _gitHubApiClient.GetLabelsForRepositoryAsync(owner, repo);

        foreach (var ghLabel in ghLabels)
        {
            var label = await _dbContext.Labels
                .FirstOrDefaultAsync(l => l.RepositoryId == repository.Id && l.Name == ghLabel.Name, cancellationToken);

            if (label == null)
            {
                label = new Label { RepositoryId = repository.Id, Name = ghLabel.Name, Color = ghLabel.Color };
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
