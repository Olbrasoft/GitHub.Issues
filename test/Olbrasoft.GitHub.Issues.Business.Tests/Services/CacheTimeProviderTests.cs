using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Unit tests for cache functionality using FakeTimeProvider.
/// These tests verify that timestamps are correctly set using TimeProvider
/// for testable and deterministic cache behavior.
/// </summary>
public class CacheTimeProviderTests
{
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly Mock<ITranslator> _mockTranslator = new();
    private readonly Mock<ITitleTranslationNotifier> _mockNotifier = new();
    private readonly Mock<ILogger<TitleTranslationService>> _mockLogger = new();
    private readonly Mock<ILogger<TitleCacheService>> _mockCacheLogger = new();

    [Fact]
    public async Task TitleTranslationService_SavesCache_WithTimeProviderTimestamp()
    {
        // Arrange - Set specific time
        var fixedTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(fixedTime);

        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Test issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        _mockTranslator.Setup(t => t.TranslateAsync(It.IsAny<string>(), "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Testovací problém",
                Provider = "Azure"
            });

        var cacheService = new TitleCacheService(
            new EfCoreTranslationRepository(context),
            _fakeTimeProvider,
            _mockCacheLogger.Object);

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            cacheService,
            _mockTranslator.Object,
            _mockNotifier.Object,
            _mockLogger.Object,
            null);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Verify cache was saved with FakeTimeProvider timestamp
        var cached = context.CachedTexts.FirstOrDefault(t => t.IssueId == issue.Id);
        Assert.NotNull(cached);
        Assert.Equal(fixedTime.UtcDateTime, cached.CachedAt);
        Assert.Equal("Testovací problém", cached.Content);
    }

    [Fact]
    public async Task TitleTranslationService_UsesCachedValue_WhenCacheFresh()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issueUpdatedAt = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var cacheCreatedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc); // Cache created AFTER issue update

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Test issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = issueUpdatedAt,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Add pre-existing cache
        var cachedText = new CachedText
        {
            IssueId = issue.Id,
            LanguageId = (int)LanguageCode.CsCZ,
            TextTypeId = (int)TextTypeCode.Title,
            Content = "Cached translation",
            CachedAt = cacheCreatedAt
        };
        context.CachedTexts.Add(cachedText);
        await context.SaveChangesAsync();

        var cacheService = new TitleCacheService(
            new EfCoreTranslationRepository(context),
            _fakeTimeProvider,
            _mockCacheLogger.Object);

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            cacheService,
            _mockTranslator.Object,
            _mockNotifier.Object,
            _mockLogger.Object,
            null);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should use cached value without calling translator
        _mockTranslator.Verify(
            t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.TranslatedTitle == "Cached translation" &&
                dto.Provider == "cache"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TitleTranslationService_ReplacesStaleCache_WhenIssueUpdatedAfterCache()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var cacheCreatedAt = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var issueUpdatedAt = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero); // Issue updated AFTER cache

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Updated issue title",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = issueUpdatedAt,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Add stale cache
        var staleCache = new CachedText
        {
            IssueId = issue.Id,
            LanguageId = (int)LanguageCode.CsCZ,
            TextTypeId = (int)TextTypeCode.Title,
            Content = "Old cached translation",
            CachedAt = cacheCreatedAt
        };
        context.CachedTexts.Add(staleCache);
        await context.SaveChangesAsync();

        var newCacheTime = new DateTimeOffset(2024, 6, 20, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(newCacheTime);

        _mockTranslator.Setup(t => t.TranslateAsync(It.IsAny<string>(), "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "New translation",
                Provider = "Azure"
            });

        var cacheService = new TitleCacheService(
            new EfCoreTranslationRepository(context),
            _fakeTimeProvider,
            _mockCacheLogger.Object);

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            cacheService,
            _mockTranslator.Object,
            _mockNotifier.Object,
            _mockLogger.Object,
            null);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should call translator and save new cache
        _mockTranslator.Verify(
            t => t.TranslateAsync("Updated issue title", "cs", null, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify new cache was saved with new timestamp
        var cachedEntries = context.CachedTexts.Where(t => t.IssueId == issue.Id).ToList();
        Assert.Single(cachedEntries);
        Assert.Equal("New translation", cachedEntries[0].Content);
        Assert.Equal(newCacheTime.UtcDateTime, cachedEntries[0].CachedAt);
    }

    [Fact]
    public async Task TitleTranslationService_SavesToCache_WhenTitleLooksCzech()
    {
        // Arrange - Title with Czech diacritics
        var fixedTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(fixedTime);

        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Přidání české funkčnosti", // Czech title - should be cached as-is
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var cacheService = new TitleCacheService(
            new EfCoreTranslationRepository(context),
            _fakeTimeProvider,
            _mockCacheLogger.Object);

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            cacheService,
            _mockTranslator.Object,
            _mockNotifier.Object,
            _mockLogger.Object,
            null);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should NOT call translator
        _mockTranslator.Verify(
            t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should save original Czech title to cache
        var cached = context.CachedTexts.FirstOrDefault(t => t.IssueId == issue.Id);
        Assert.NotNull(cached);
        Assert.Equal("Přidání české funkčnosti", cached.Content);
        Assert.Equal(fixedTime.UtcDateTime, cached.CachedAt);

        // Should notify with "original" provider
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto => dto.Provider == "original"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TimeProvider_AdvanceTime_AffectsCacheTimestamp()
    {
        // Arrange - Start at specific time
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(startTime);

        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Test issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        _mockTranslator.Setup(t => t.TranslateAsync(It.IsAny<string>(), "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Testovací problém",
                Provider = "Azure"
            });

        // Advance time by 30 days
        _fakeTimeProvider.Advance(TimeSpan.FromDays(30));

        var cacheService = new TitleCacheService(
            new EfCoreTranslationRepository(context),
            _fakeTimeProvider,
            _mockCacheLogger.Object);

        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            cacheService,
            _mockTranslator.Object,
            _mockNotifier.Object,
            _mockLogger.Object,
            null);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Cache should have the advanced time
        var cached = context.CachedTexts.FirstOrDefault(t => t.IssueId == issue.Id);
        Assert.NotNull(cached);
        Assert.Equal(startTime.AddDays(30).UtcDateTime, cached.CachedAt);
    }

    private static GitHubDbContext CreateInMemoryContext()
    {
        return Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();
    }
}
