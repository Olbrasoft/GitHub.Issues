using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Integration tests for cache functionality.
/// These tests verify the full cache workflow including database operations.
/// Skipped on GitHub CI to avoid dependencies on external services.
/// </summary>
public class CacheIntegrationTests
{
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly Mock<ITranslator> _mockTranslator = new();
    private readonly Mock<ITitleTranslationNotifier> _mockNotifier = new();
    private readonly Mock<ILogger<TitleTranslationService>> _mockTitleLogger = new();
    private readonly Mock<ISummarizationService> _mockSummarizationService = new();
    private readonly Mock<ITranslationFallbackService> _mockTranslationService = new();
    private readonly Mock<ISummaryNotifier> _mockSummaryNotifier = new();
    private readonly Mock<ILogger<IssueSummaryService>> _mockSummaryLogger = new();

    /// <summary>
    /// Tests full cache lifecycle: miss → save → hit workflow for titles.
    /// </summary>
    [SkipOnCIFact]
    public async Task TitleCache_FullLifecycle_MissSaveHit()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(fixedTime);

        await using var context = CreateInMemoryContext();
        await SeedReferenceDataAsync(context);

        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "Implement new feature",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = fixedTime.AddDays(-1),
            SyncedAt = fixedTime,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        _mockTranslator.Setup(t => t.TranslateAsync("Implement new feature", "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Implementovat novou funkci",
                Provider = "Azure"
            });

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            _mockTranslator.Object,
            _mockNotifier.Object,
            _fakeTimeProvider,
            _mockTitleLogger.Object,
            null);

        // Act 1: First call - cache miss
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert 1: Translator was called
        _mockTranslator.Verify(
            t => t.TranslateAsync("Implement new feature", "cs", null, It.IsAny<CancellationToken>()),
            Times.Once);

        // Reset mock
        _mockTranslator.Invocations.Clear();

        // Act 2: Second call - cache hit
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert 2: Translator was NOT called (cache hit)
        _mockTranslator.Verify(
            t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify notification was sent with "cache" provider
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto => dto.Provider == "cache"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests cache invalidation when issue is updated on GitHub.
    /// </summary>
    [SkipOnCIFact]
    public async Task TitleCache_InvalidatesOnIssueUpdate()
    {
        // Arrange
        var cacheTime = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(cacheTime);

        await using var context = CreateInMemoryContext();
        await SeedReferenceDataAsync(context);

        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        // Issue was updated BEFORE cache was created (cache is fresh)
        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "Original title",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = cacheTime.AddDays(-1), // Issue updated BEFORE cache
            SyncedAt = cacheTime,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        _mockTranslator.Setup(t => t.TranslateAsync(It.IsAny<string>(), "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Překlad",
                Provider = "Azure"
            });

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            _mockTranslator.Object,
            _mockNotifier.Object,
            _fakeTimeProvider,
            _mockTitleLogger.Object,
            null);

        // First call - creates cache
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);
        _mockTranslator.Invocations.Clear();

        // Simulate GitHub update (issue updated AFTER cache)
        issue.Title = "Updated title from GitHub";
        issue.GitHubUpdatedAt = cacheTime.AddDays(5); // Issue now updated AFTER cache
        await context.SaveChangesAsync();

        // Act: Call again - should invalidate stale cache
        _fakeTimeProvider.SetUtcNow(cacheTime.AddDays(6));
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert: Translator was called again (cache was stale)
        _mockTranslator.Verify(
            t => t.TranslateAsync("Updated title from GitHub", "cs", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that multiple languages are cached independently.
    /// </summary>
    [SkipOnCIFact]
    public async Task TitleCache_CachesMultipleLanguagesIndependently()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(fixedTime);

        await using var context = CreateInMemoryContext();
        await SeedReferenceDataAsync(context);

        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "New feature",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = fixedTime.AddDays(-1),
            SyncedAt = fixedTime,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        _mockTranslator
            .Setup(t => t.TranslateAsync("New feature", "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = true, Translation = "Nová funkce", Provider = "Azure" });

        _mockTranslator
            .Setup(t => t.TranslateAsync("New feature", "de", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = true, Translation = "Neue Funktion", Provider = "Azure" });

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            _mockTranslator.Object,
            _mockNotifier.Object,
            _fakeTimeProvider,
            _mockTitleLogger.Object,
            null);

        // Act: Translate to Czech and German
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);
        await service.TranslateTitleAsync(issue.Id, "de", CancellationToken.None);

        // Assert: Both translations are cached independently
        var cachedEntries = context.CachedTexts.Where(t => t.IssueId == issue.Id).ToList();
        Assert.Equal(2, cachedEntries.Count);

        var csCached = cachedEntries.FirstOrDefault(t => t.LanguageId == (int)LanguageCode.CsCZ);
        var deCached = cachedEntries.FirstOrDefault(t => t.LanguageId == (int)LanguageCode.DeDE);

        Assert.NotNull(csCached);
        Assert.NotNull(deCached);
        Assert.Equal("Nová funkce", csCached.Content);
        Assert.Equal("Neue Funktion", deCached.Content);
    }

    /// <summary>
    /// Tests TranslationCacheService invalidation methods.
    /// Note: ExecuteDeleteAsync is not supported by in-memory database.
    /// This test requires a real database (PostgreSQL/SQL Server).
    /// </summary>
    [Fact(Skip = "ExecuteDeleteAsync not supported by in-memory database - requires real database")]
    public async Task TranslationCacheService_InvalidatesCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        await SeedReferenceDataAsync(context);

        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue1 = new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", IsOpen = true, Url = "url", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = [] };
        var issue2 = new Issue { RepositoryId = repo.Id, Number = 2, Title = "Issue 2", IsOpen = true, Url = "url", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = [] };
        context.Issues.AddRange(issue1, issue2);
        await context.SaveChangesAsync();

        // Add cache entries
        context.CachedTexts.AddRange(
            new CachedText { IssueId = issue1.Id, LanguageId = (int)LanguageCode.CsCZ, TextTypeId = (int)TextTypeCode.Title, Content = "Title 1 CS", CachedAt = DateTime.UtcNow },
            new CachedText { IssueId = issue1.Id, LanguageId = (int)LanguageCode.CsCZ, TextTypeId = (int)TextTypeCode.ListSummary, Content = "Summary 1 CS", CachedAt = DateTime.UtcNow },
            new CachedText { IssueId = issue2.Id, LanguageId = (int)LanguageCode.CsCZ, TextTypeId = (int)TextTypeCode.Title, Content = "Title 2 CS", CachedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repository = new EfCoreCachedTextRepository(context);
        var mockLogger = new Mock<ILogger<TranslationCacheService>>();
        var cacheService = new TranslationCacheService(repository, mockLogger.Object);

        // Act 1: Invalidate single issue
        var deleted = await cacheService.InvalidateAsync(issue1.Id);

        // Assert 1: Only issue1 cache entries are deleted
        Assert.Equal(2, deleted);
        Assert.Single(context.CachedTexts); // Only issue2 cache remains
        Assert.Equal(issue2.Id, context.CachedTexts.First().IssueId);
    }

    /// <summary>
    /// Tests TranslationCacheService statistics.
    /// Note: Requires seeded Language and TextType entities with navigation properties.
    /// </summary>
    [Fact(Skip = "Requires navigation property access not fully supported by in-memory database")]
    public async Task TranslationCacheService_ReturnsCorrectStatistics()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        await SeedReferenceDataAsync(context);

        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", IsOpen = true, Url = "url", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = [] };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Add cache entries
        context.CachedTexts.AddRange(
            new CachedText { IssueId = issue.Id, LanguageId = (int)LanguageCode.CsCZ, TextTypeId = (int)TextTypeCode.Title, Content = "Title CS", CachedAt = DateTime.UtcNow },
            new CachedText { IssueId = issue.Id, LanguageId = (int)LanguageCode.EnUS, TextTypeId = (int)TextTypeCode.Title, Content = "Title EN", CachedAt = DateTime.UtcNow },
            new CachedText { IssueId = issue.Id, LanguageId = (int)LanguageCode.CsCZ, TextTypeId = (int)TextTypeCode.ListSummary, Content = "Summary CS", CachedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repository = new EfCoreCachedTextRepository(context);
        var mockLogger = new Mock<ILogger<TranslationCacheService>>();
        var cacheService = new TranslationCacheService(repository, mockLogger.Object);

        // Act
        var stats = await cacheService.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRecords);
        Assert.Equal(2, stats.ByLanguage.Count); // cs-CZ and en-US
        Assert.Equal(2, stats.ByTextType.Count); // Title and ListSummary
    }

    /// <summary>
    /// Seeds reference data (Languages, TextTypes) required by foreign key constraints.
    /// </summary>
    private static async Task SeedReferenceDataAsync(GitHubDbContext context)
    {
        // Seed Languages if not present
        if (!context.Languages.Any())
        {
            context.Languages.AddRange(
                new Language { Id = (int)LanguageCode.CsCZ, CultureName = "cs-CZ" },
                new Language { Id = (int)LanguageCode.EnUS, CultureName = "en-US" },
                new Language { Id = (int)LanguageCode.DeDE, CultureName = "de-DE" }
            );
        }

        // Seed TextTypes if not present
        if (!context.TextTypes.Any())
        {
            context.TextTypes.AddRange(
                new TextType { Id = (int)TextTypeCode.Title, Name = "Title" },
                new TextType { Id = (int)TextTypeCode.ListSummary, Name = "ListSummary" },
                new TextType { Id = (int)TextTypeCode.DetailSummary, Name = "DetailSummary" }
            );
        }

        await context.SaveChangesAsync();
    }

    private static GitHubDbContext CreateInMemoryContext()
    {
        return Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();
    }
}
