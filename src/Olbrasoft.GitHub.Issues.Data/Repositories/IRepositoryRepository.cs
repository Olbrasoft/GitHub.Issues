using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for Repository entity operations.
/// Follows Dependency Inversion Principle - Business layer depends on this abstraction.
/// </summary>
public interface IRepositoryRepository
{
    /// <summary>
    /// Gets the total count of repositories in the database.
    /// Used for database status checking and statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of repositories</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a repository by its ID.
    /// </summary>
    /// <param name="repositoryId">Internal repository ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Repository entity or null if not found</returns>
    Task<Repository?> GetByIdAsync(int repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a repository by its GitHub ID.
    /// </summary>
    /// <param name="gitHubId">GitHub repository ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Repository entity or null if not found</returns>
    Task<Repository?> GetByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a repository by its full name (owner/repo).
    /// </summary>
    /// <param name="fullName">Repository full name (e.g., "Olbrasoft/GitHub.Issues")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Repository entity or null if not found</returns>
    Task<Repository?> GetByFullNameAsync(string fullName, CancellationToken cancellationToken = default);
}
