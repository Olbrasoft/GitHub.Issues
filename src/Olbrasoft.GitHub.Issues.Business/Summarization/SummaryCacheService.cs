using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Implementation of summary cache service.
/// Handles caching of AI-generated summaries with freshness validation.
/// </summary>
public class SummaryCacheService : ISummaryCacheService
{
    private readonly ICachedTextRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SummaryCacheService> _logger;

    public SummaryCacheService(
        ICachedTextRepository repository,
        TimeProvider timeProvider,
        ILogger<SummaryCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string?> GetIfFreshAsync(
        int issueId,
        int languageCode,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var textTypeId = (int)TextTypeCode.ListSummary;

        var cachedSummary = await _repository.GetIfFreshAsync(
            issueId,
            languageCode,
            textTypeId,
            issueUpdatedAt,
            cancellationToken);

        if (cachedSummary != null)
        {
            _logger.LogDebug(
                "[SummaryCacheService] Cache HIT for issue {IssueId}, language {LanguageCode}",
                issueId,
                languageCode);
        }
        else
        {
            _logger.LogDebug(
                "[SummaryCacheService] Cache MISS for issue {IssueId}, language {LanguageCode}",
                issueId,
                languageCode);
        }

        return cachedSummary;
    }

    public async Task SaveAsync(
        int issueId,
        int languageCode,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var textTypeId = (int)TextTypeCode.ListSummary;

        await _repository.SaveAsync(new CachedText
        {
            IssueId = issueId,
            LanguageId = languageCode,
            TextTypeId = textTypeId,
            Content = summary,
            CachedAt = _timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);

        _logger.LogDebug(
            "[SummaryCacheService] Saved to cache: Issue {IssueId}, Language {LanguageCode}",
            issueId,
            languageCode);
    }
}
