using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Integration tests for IssueDetailService summary translation.
/// These tests verify the translation flow with primary translator (Azure) and fallback behavior.
/// Note: DeepL fallback cannot be unit-tested with mocks because DeepLTranslator is a concrete class.
/// Fallback behavior is verified via integration tests on Azure.
/// </summary>
public class IssueDetailServiceSummaryTranslationTests
{
    private readonly Mock<IGitHubGraphQLClient> _graphQLClientMock;
    private readonly Mock<ISummarizationService> _summarizationServiceMock;
    private readonly Mock<ITranslator> _primaryTranslatorMock;
    private readonly Mock<ISummaryNotifier> _summaryNotifierMock;
    private readonly Mock<IBodyNotifier> _bodyNotifierMock;
    private readonly Mock<ILogger<IssueDetailService>> _loggerMock;
    private readonly BodyPreviewSettings _bodyPreviewSettings;

    public IssueDetailServiceSummaryTranslationTests()
    {
        _graphQLClientMock = new Mock<IGitHubGraphQLClient>();
        _summarizationServiceMock = new Mock<ISummarizationService>();
        _primaryTranslatorMock = new Mock<ITranslator>();
        _summaryNotifierMock = new Mock<ISummaryNotifier>();
        _bodyNotifierMock = new Mock<IBodyNotifier>();
        _loggerMock = new Mock<ILogger<IssueDetailService>>();
        _bodyPreviewSettings = new BodyPreviewSettings { MaxLength = 500 };
    }

    /// <summary>
    /// Verifies that when NO fallback translator is provided and primary (Azure) fails,
    /// English summary is used as fallback with "(EN fallback)" marker.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenNoFallbackAndPrimaryFails_SendsEnglishAsFallback()
    {
        // Arrange
        const int issueId = 123;
        const string body = "This is the issue body.";
        const string englishSummary = "Summary of the issue.";

        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "OpenAI",
                Model = "gpt-4"
            });

        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "No API key" });

        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        // Create service WITHOUT fallback translator
        using var dbContext = CreateInMemoryDbContext();
        var service = new IssueDetailService(
            dbContext,
            _graphQLClientMock.Object,
            _summarizationServiceMock.Object,
            _primaryTranslatorMock.Object,
            _summaryNotifierMock.Object,
            _bodyNotifierMock.Object,
            Options.Create(_bodyPreviewSettings),
            _loggerMock.Object,
            fallbackTranslator: null); // No fallback!

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert - Should send English as fallback
        Assert.NotNull(capturedNotification);
        Assert.Equal(englishSummary, capturedNotification.Summary);
        Assert.Equal("en", capturedNotification.Language); // Falls back to English
        Assert.Contains("EN fallback", capturedNotification.Provider ?? "");
    }

    /// <summary>
    /// Verifies that when primary translator succeeds, Czech summary is sent.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenPrimarySucceeds_SendsCzechSummary()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body content.";
        const string englishSummary = "Summary in English.";
        const string czechSummary = "Shrnutí v češtině.";

        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "OpenAI",
                Model = "gpt-4"
            });

        // Primary translator succeeds
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = czechSummary,
                Provider = "Azure"
            });

        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryDbContext();
        var service = new IssueDetailService(
            dbContext,
            _graphQLClientMock.Object,
            _summarizationServiceMock.Object,
            _primaryTranslatorMock.Object,
            _summaryNotifierMock.Object,
            _bodyNotifierMock.Object,
            Options.Create(_bodyPreviewSettings),
            _loggerMock.Object,
            fallbackTranslator: null);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert - Should receive Czech summary
        Assert.NotNull(capturedNotification);
        Assert.Equal(czechSummary, capturedNotification.Summary);
        Assert.Equal("cs", capturedNotification.Language);
        Assert.Contains("Azure", capturedNotification.Provider ?? "");
    }

    /// <summary>
    /// Verifies that for English language, no translation is attempted.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenLanguageIsEnglish_DoesNotTranslate()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body.";
        const string englishSummary = "English summary.";

        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "OpenAI",
                Model = "gpt-4"
            });

        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryDbContext();
        var service = new IssueDetailService(
            dbContext,
            _graphQLClientMock.Object,
            _summarizationServiceMock.Object,
            _primaryTranslatorMock.Object,
            _summaryNotifierMock.Object,
            _bodyNotifierMock.Object,
            Options.Create(_bodyPreviewSettings),
            _loggerMock.Object,
            fallbackTranslator: null);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert - No translation should be attempted
        _primaryTranslatorMock.Verify(
            x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should receive English summary
        Assert.NotNull(capturedNotification);
        Assert.Equal(englishSummary, capturedNotification.Summary);
        Assert.Equal("en", capturedNotification.Language);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenBothModeAndTranslationFails_SendsEnglishAsCzechFallback()
    {
        // Arrange
        const int issueId = 1;
        const string body = "This is a test issue body";
        const string englishSummary = "Summary of the issue";

        // Setup summarization to succeed
        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "TestProvider",
                Model = "TestModel"
            });

        // Setup primary translator to FAIL
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "API key not configured" });

        // Capture notifications
        var notifications = new List<SummaryNotificationDto>();
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto))
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryDbContext();
        var service = new IssueDetailService(
            dbContext,
            _graphQLClientMock.Object,
            _summarizationServiceMock.Object,
            _primaryTranslatorMock.Object,
            _summaryNotifierMock.Object,
            _bodyNotifierMock.Object,
            Options.Create(_bodyPreviewSettings),
            _loggerMock.Object,
            fallbackTranslator: null); // No DeepL fallback for this test

        // Act - Use "both" language mode
        await service.GenerateSummaryFromBodyAsync(issueId, body, "both");

        // Assert - Should have 2 notifications: English first, then Czech fallback
        Assert.Equal(2, notifications.Count);

        // First notification should be English summary
        Assert.Equal(englishSummary, notifications[0].Summary);
        Assert.Equal("en", notifications[0].Language);

        // Second notification should be English text with "cs" language (fallback)
        Assert.Equal(englishSummary, notifications[1].Summary);
        Assert.Equal("cs", notifications[1].Language);
        Assert.Contains("překlad nedostupný", notifications[1].Provider);
    }

    /// <summary>
    /// Verifies that "both" mode sends English first, then Czech.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenBothModeAndTranslationSucceeds_SendsBothLanguages()
    {
        // Arrange
        const int issueId = 1;
        const string body = "Issue body content";
        const string englishSummary = "English summary";
        const string czechSummary = "České shrnutí";

        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "TestProvider",
                Model = "TestModel"
            });

        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = czechSummary,
                Provider = "Azure"
            });

        var notifications = new List<SummaryNotificationDto>();
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto))
            .Returns(Task.CompletedTask);

        using var dbContext = CreateInMemoryDbContext();
        var service = new IssueDetailService(
            dbContext,
            _graphQLClientMock.Object,
            _summarizationServiceMock.Object,
            _primaryTranslatorMock.Object,
            _summaryNotifierMock.Object,
            _bodyNotifierMock.Object,
            Options.Create(_bodyPreviewSettings),
            _loggerMock.Object,
            fallbackTranslator: null);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "both");

        // Assert - Should have 2 notifications
        Assert.Equal(2, notifications.Count);

        // First: English
        Assert.Equal(englishSummary, notifications[0].Summary);
        Assert.Equal("en", notifications[0].Language);

        // Second: Czech
        Assert.Equal(czechSummary, notifications[1].Summary);
        Assert.Equal("cs", notifications[1].Language);
    }

    private static GitHubDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GitHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GitHubDbContext(options);
    }
}
