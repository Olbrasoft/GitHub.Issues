using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing translation cache invalidation.
/// </summary>
public class TranslationCacheService : ITranslationCacheService
{
    private readonly ICachedTextRepository _repository;
    private readonly ILogger<TranslationCacheService> _logger;

    public TranslationCacheService(
        ICachedTextRepository repository,
        ILogger<TranslationCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateAsync(int issueId, CancellationToken ct = default)
    {
        var deleted = await _repository.InvalidateByIssueAsync(issueId, ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "[TranslationCache] Invalidated {Count} cached translations for Issue {IssueId}",
                deleted, issueId);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByTextTypeAsync(int issueId, int textTypeId, CancellationToken ct = default)
    {
        var deleted = await _repository.InvalidateByIssueAndTextTypeAsync(issueId, textTypeId, ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "[TranslationCache] Invalidated {Count} cached translations for Issue {IssueId}, TextType {TextTypeId}",
                deleted, issueId, textTypeId);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken ct = default)
    {
        var repoIds = repositoryIds.ToList();
        var deleted = await _repository.InvalidateByRepositoriesAsync(repoIds, ct);

        _logger.LogInformation(
            "[TranslationCache] Admin cleared {Count} cache entries for {RepoCount} repositories",
            deleted, repoIds.Count);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> InvalidateAllAsync(CancellationToken ct = default)
    {
        var deleted = await _repository.InvalidateAllAsync(ct);

        _logger.LogWarning(
            "[TranslationCache] Admin cleared ENTIRE translation cache: {Count} entries",
            deleted);

        return deleted;
    }

    /// <inheritdoc />
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        return await _repository.GetStatisticsAsync(ct);
    }
}
