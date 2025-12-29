using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for Label entity operations.
/// Follows Dependency Inversion Principle - Business layer depends on this abstraction.
/// </summary>
public interface ILabelRepository
{
    /// <summary>
    /// Gets all labels for a specific repository.
    /// </summary>
    /// <param name="repositoryId">Internal repository ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of labels for the repository</returns>
    Task<List<Label>> GetByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a label by its ID.
    /// </summary>
    /// <param name="labelId">Internal label ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Label entity or null if not found</returns>
    Task<Label?> GetByIdAsync(int labelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a label by its name within a specific repository.
    /// </summary>
    /// <param name="repositoryId">Internal repository ID</param>
    /// <param name="name">Label name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Label entity or null if not found</returns>
    Task<Label?> GetByNameAsync(int repositoryId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of labels in the database.
    /// Used for database status checking and statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of labels</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
