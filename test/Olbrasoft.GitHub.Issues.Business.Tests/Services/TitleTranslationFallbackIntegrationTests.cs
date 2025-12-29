using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.DeepL;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Integration tests for TitleTranslationService fallback mechanism.
/// These tests verify that when primary translator (Azure) fails,
/// the service falls back to DeepL translator.
/// </summary>
public class TitleTranslationFallbackIntegrationTests
{
    private readonly Mock<ITitleTranslationNotifier> _mockNotifier = new();
    private readonly Mock<ILogger<TitleTranslationService>> _mockLogger = new();
    private readonly Mock<ILogger<DeepLTranslator>> _mockDeepLLogger = new();

    /// <summary>
    /// Tests that when primary translator fails, DeepL fallback is used.
    /// This is a real integration test that calls DeepL API.
    /// Skipped on CI - runs only locally where DEEPL_API_KEY is available.
    /// </summary>
    [SkipOnCIFact]
    public async Task TranslateTitleAsync_WhenPrimaryFails_UsesFallback_Integration()
    {
        // Arrange - Create failing primary translator
        var mockPrimaryTranslator = new Mock<ITranslator>();
        mockPrimaryTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail("Azure Translator API error: 401 Unauthorized", "Azure"));

        // Arrange - Create real DeepL translator with test API key
        var deepLApiKey = Environment.GetEnvironmentVariable("DEEPL_API_KEY") ?? "96470ca9-c69b-4f13-99d6-3f49b76af4cd:fx";
        var deepLSettings = new DeepLSettings
        {
            ApiKey = deepLApiKey,
            Endpoint = "https://api-free.deepl.com/v2/"
        };
        var deepLOptions = Options.Create(deepLSettings);
        var httpClient = new HttpClient();
        var deepLTranslator = new DeepLTranslator(httpClient, deepLOptions, _mockDeepLLogger.Object);

        // Arrange - Create in-memory database with test issue
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Add new feature for user authentication",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Arrange - Create service with failing primary and real DeepL fallback
        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            mockPrimaryTranslator.Object,
            _mockNotifier.Object,
            TimeProvider.System,
            _mockLogger.Object,
            deepLTranslator);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should notify with translation from DeepL
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.Provider == "DeepL" &&
                !string.IsNullOrEmpty(dto.TranslatedTitle) &&
                dto.TranslatedTitle != "Add new feature for user authentication"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Unit test verifying fallback logic with mocked ITranslator.
    /// </summary>
    [Fact]
    public async Task TranslateTitleAsync_WhenPrimaryFails_UsesMockedFallback()
    {
        // Arrange - Create failing primary translator
        var mockPrimaryTranslator = new Mock<ITranslator>();
        mockPrimaryTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail("Azure error", "Azure"));

        // Arrange - Create mock fallback translator (ITranslator) that succeeds
        var mockFallbackTranslator = new Mock<ITranslator>();
        mockFallbackTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Ok("Přidat novou funkci", "DeepL", "en"));

        // Arrange - Create in-memory database with test issue
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Add new feature",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Arrange - Create service with failing primary and mocked fallback
        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            mockPrimaryTranslator.Object,
            _mockNotifier.Object,
            TimeProvider.System,
            _mockLogger.Object,
            mockFallbackTranslator.Object);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should notify with translation from fallback
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.Provider == "DeepL" &&
                dto.TranslatedTitle == "Přidat novou funkci"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that when both primary and fallback fail, original title is used.
    /// </summary>
    [Fact]
    public async Task TranslateTitleAsync_WhenBothFail_NotifiesWithOriginal()
    {
        // Arrange - Create failing primary translator
        var mockPrimaryTranslator = new Mock<ITranslator>();
        mockPrimaryTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail("Azure error", "Azure"));

        // Arrange - Create failing fallback translator
        var mockFallbackTranslator = new Mock<ITranslator>();
        mockFallbackTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail("DeepL error", "DeepL"));

        // Arrange - Create in-memory database with test issue
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Add new feature",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Arrange - Create service with both failing
        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            mockPrimaryTranslator.Object,
            _mockNotifier.Object,
            TimeProvider.System,
            _mockLogger.Object,
            mockFallbackTranslator.Object);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should notify with original title and provider=failed
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.Provider == "failed" &&
                dto.TranslatedTitle == "Add new feature"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that when no fallback is provided and primary fails, original title is used.
    /// </summary>
    [Fact]
    public async Task TranslateTitleAsync_WhenNoFallbackAndPrimaryFails_NotifiesWithOriginal()
    {
        // Arrange - Create failing primary translator
        var mockPrimaryTranslator = new Mock<ITranslator>();
        mockPrimaryTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail("Azure error", "Azure"));

        // Arrange - Create in-memory database with test issue
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Add new feature",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        // Arrange - Create service WITHOUT fallback (null)
        var service = new TitleTranslationService(
            new EfCoreTranslationRepository(context),
            mockPrimaryTranslator.Object,
            _mockNotifier.Object,
            TimeProvider.System,
            _mockLogger.Object,
            null);  // No fallback!

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should notify with original title and provider=failed
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.Provider == "failed" &&
                dto.TranslatedTitle == "Add new feature"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GitHubDbContext CreateInMemoryContext()
    {
        return Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();
    }
}
