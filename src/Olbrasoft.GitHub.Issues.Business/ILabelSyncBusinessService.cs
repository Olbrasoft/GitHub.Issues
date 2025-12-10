using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Business service interface for label sync operations.
/// </summary>
public interface ILabelSyncBusinessService
{
    /// <summary>
    /// Gets a label by repository ID and name.
    /// </summary>
    Task<Label?> GetLabelAsync(int repositoryId, string name, CancellationToken ct = default);

    /// <summary>
    /// Gets all labels for a repository.
    /// </summary>
    Task<List<Label>> GetLabelsByRepositoryAsync(int repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Saves (creates or updates) a label.
    /// </summary>
    Task<Label> SaveLabelAsync(int repositoryId, string name, string color, CancellationToken ct = default);
}
