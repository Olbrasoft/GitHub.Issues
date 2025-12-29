using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Business.Search;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Business.Database;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class TitleTranslationServiceTests
{
    private readonly Mock<ITranslator> _mockTranslator = new();
    private readonly Mock<ITitleTranslationNotifier> _mockNotifier = new();
    private readonly Mock<ILogger<TitleTranslationService>> _mockLogger = new();

    private TitleTranslationService CreateService(GitHubDbContext context, ITranslator? fallback = null)
    {
        var repository = new EfCoreTranslationRepository(context);
        return new TitleTranslationService(
            repository,
            _mockTranslator.Object,
            _mockNotifier.Object,
            TimeProvider.System,
            _mockLogger.Object,
            fallback);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenTargetLanguageIsEnglish_SkipsTranslation()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(1, "en", CancellationToken.None);

        // Assert
        _mockTranslator.Verify(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(It.IsAny<TitleTranslationNotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenIssueNotFound_DoesNotTranslate()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(999, "cs", CancellationToken.None);

        // Assert
        _mockTranslator.Verify(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenTitleAlreadyLooksCzech_UsesOriginal()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Přidání české funkčnosti pro projekt",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert - Should not call translator, but should notify with original
        _mockTranslator.Verify(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto => dto.Provider == "original"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenTranslationSucceeds_NotifiesWithTranslation()
    {
        // Arrange
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

        _mockTranslator.Setup(t => t.TranslateAsync("Add new feature", "cs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Přidat novou funkci",
                Provider = "Azure"
            });

        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.TranslatedTitle == "Přidat novou funkci" &&
                dto.Provider == "Azure"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenTranslationFails_NotifiesWithOriginal()
    {
        // Arrange
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

        _mockTranslator.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = false,
                Error = "Translation failed"
            });

        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(issue.Id, "cs", CancellationToken.None);

        // Assert
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto =>
                dto.TranslatedTitle == "Add new feature" &&
                dto.Provider == "failed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateTitleAsync_WhenTitleLooksGerman_UsesOriginalForGermanTarget()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Änderung für größere Verbesserung",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        await service.TranslateTitleAsync(issue.Id, "de", CancellationToken.None);

        // Assert - Should not call translator, but should notify with original
        _mockTranslator.Verify(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotifier.Verify(n => n.NotifyTitleTranslatedAsync(
            It.Is<TitleTranslationNotificationDto>(dto => dto.Provider == "original"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GitHubDbContext CreateInMemoryContext()
    {
        return Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();
    }
}
