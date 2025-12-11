using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for translating issue titles to Czech using AI providers.
/// Caches translations in the database for reuse.
/// </summary>
public class TitleTranslationService : ITitleTranslationService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IAiTranslationService _translationService;
    private readonly ILogger<TitleTranslationService> _logger;

    // Cache validity period - regenerate translation if issue was updated after cache
    private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromDays(30);

    public TitleTranslationService(
        GitHubDbContext dbContext,
        IAiTranslationService translationService,
        ILogger<TitleTranslationService> logger)
    {
        _dbContext = dbContext;
        _translationService = translationService;
        _logger = logger;
    }

    public async Task<TitleTranslationResult> TranslateTitleAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);

        if (issue == null)
        {
            return new TitleTranslationResult(issueId, null, false, "Issue not found");
        }

        // Check if cached translation is still valid
        var isCacheValid = issue.TitleTranslatedAt.HasValue &&
                          issue.TitleTranslatedAt.Value > issue.GitHubUpdatedAt &&
                          issue.TitleTranslatedAt.Value > DateTimeOffset.UtcNow.Subtract(CacheValidityPeriod);

        if (isCacheValid && !string.IsNullOrWhiteSpace(issue.CzechTitle))
        {
            _logger.LogDebug("Using cached Czech title for issue {Id}", issueId);
            return new TitleTranslationResult(issueId, issue.CzechTitle, true);
        }

        // Translate the title
        var translateResult = await _translationService.TranslateToCzechAsync(issue.Title, cancellationToken);

        if (!translateResult.Success || string.IsNullOrWhiteSpace(translateResult.Translation))
        {
            _logger.LogWarning("Failed to translate title for issue {Id}: {Error}", issueId, translateResult.Error);
            return new TitleTranslationResult(issueId, null, false, translateResult.Error);
        }

        // Cache the translation
        issue.CzechTitle = translateResult.Translation;
        issue.TitleTranslatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Cached Czech title for issue {Id}", issueId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache title translation for issue {Id}", issueId);
            // Still return success - translation worked, just caching failed
        }

        return new TitleTranslationResult(issueId, translateResult.Translation, true);
    }

    public async Task<IReadOnlyList<TitleTranslationResult>> TranslateTitlesAsync(
        IReadOnlyList<int> issueIds,
        CancellationToken cancellationToken = default)
    {
        if (issueIds.Count == 0)
        {
            return Array.Empty<TitleTranslationResult>();
        }

        var results = new List<TitleTranslationResult>(issueIds.Count);

        // Load all issues at once for efficiency
        var issues = await _dbContext.Issues
            .Where(i => issueIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var issueMap = issues.ToDictionary(i => i.Id);
        var now = DateTimeOffset.UtcNow;
        var cacheThreshold = now.Subtract(CacheValidityPeriod);

        foreach (var issueId in issueIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!issueMap.TryGetValue(issueId, out var issue))
            {
                results.Add(new TitleTranslationResult(issueId, null, false, "Issue not found"));
                continue;
            }

            // Check if cached translation is still valid
            var isCacheValid = issue.TitleTranslatedAt.HasValue &&
                              issue.TitleTranslatedAt.Value > issue.GitHubUpdatedAt &&
                              issue.TitleTranslatedAt.Value > cacheThreshold;

            if (isCacheValid && !string.IsNullOrWhiteSpace(issue.CzechTitle))
            {
                _logger.LogDebug("Using cached Czech title for issue {Id}", issueId);
                results.Add(new TitleTranslationResult(issueId, issue.CzechTitle, true));
                continue;
            }

            // Translate the title
            var translateResult = await _translationService.TranslateToCzechAsync(issue.Title, cancellationToken);

            if (!translateResult.Success || string.IsNullOrWhiteSpace(translateResult.Translation))
            {
                _logger.LogWarning("Failed to translate title for issue {Id}: {Error}", issueId, translateResult.Error);
                results.Add(new TitleTranslationResult(issueId, null, false, translateResult.Error));
                continue;
            }

            // Cache the translation
            issue.CzechTitle = translateResult.Translation;
            issue.TitleTranslatedAt = now;

            results.Add(new TitleTranslationResult(issueId, translateResult.Translation, true));
        }

        // Save all cached translations at once
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Cached {Count} title translations", results.Count(r => r.Success));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache title translations");
        }

        return results;
    }
}
