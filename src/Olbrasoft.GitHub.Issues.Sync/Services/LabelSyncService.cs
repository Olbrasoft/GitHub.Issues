using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing labels from GitHub.
/// </summary>
public class LabelSyncService : ILabelSyncService
{
    private readonly ILabelSyncBusinessService _labelSyncBusiness;
    private readonly IGitHubApiClient _gitHubApiClient;
    private readonly ILogger<LabelSyncService> _logger;

    public LabelSyncService(
        ILabelSyncBusinessService labelSyncBusiness,
        IGitHubApiClient gitHubApiClient,
        ILogger<LabelSyncService> logger)
    {
        _labelSyncBusiness = labelSyncBusiness;
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
            var label = await _labelSyncBusiness.GetLabelAsync(repository.Id, ghLabel.Name, cancellationToken);

            if (label == null)
            {
                await _labelSyncBusiness.SaveLabelAsync(repository.Id, ghLabel.Name, ghLabel.Color, cancellationToken);
                _logger.LogDebug("Created label: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
            else if (label.Color != ghLabel.Color)
            {
                await _labelSyncBusiness.SaveLabelAsync(repository.Id, ghLabel.Name, ghLabel.Color, cancellationToken);
                _logger.LogDebug("Updated label color: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
        }
    }
}
