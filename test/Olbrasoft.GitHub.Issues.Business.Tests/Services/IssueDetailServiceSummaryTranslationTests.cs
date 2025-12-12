using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Integration tests for IssueDetailService summary translation.
/// These tests verify that when language is Czech, summaries are translated
/// using the fallback translator (Cohere) when DeepL fails.
/// </summary>
public class IssueDetailServiceSummaryTranslationTests
{
    private readonly Mock<IGitHubGraphQLClient> _graphQLClientMock;
    private readonly Mock<ISummarizationService> _summarizationServiceMock;
    private readonly Mock<ITranslator> _primaryTranslatorMock;
    private readonly Mock<ITranslationService> _fallbackTranslatorMock;
    private readonly Mock<ISummaryNotifier> _summaryNotifierMock;
    private readonly Mock<IBodyNotifier> _bodyNotifierMock;
    private readonly Mock<ILogger<IssueDetailService>> _loggerMock;
    private readonly BodyPreviewSettings _bodyPreviewSettings;

    public IssueDetailServiceSummaryTranslationTests()
    {
        _graphQLClientMock = new Mock<IGitHubGraphQLClient>();
        _summarizationServiceMock = new Mock<ISummarizationService>();
        _primaryTranslatorMock = new Mock<ITranslator>();
        _fallbackTranslatorMock = new Mock<ITranslationService>();
        _summaryNotifierMock = new Mock<ISummaryNotifier>();
        _bodyNotifierMock = new Mock<IBodyNotifier>();
        _loggerMock = new Mock<ILogger<IssueDetailService>>();
        _bodyPreviewSettings = new BodyPreviewSettings { MaxLength = 500 };
    }

    /// <summary>
    /// CRITICAL TEST: Verifies that when primary translator (DeepL) fails,
    /// the fallback translator (Cohere) is used and Czech summary is sent.
    /// This test should FAIL if the fallback mechanism doesn't work.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenPrimaryTranslatorFails_UsesFallbackAndSendsCzechSummary()
    {
        // Arrange
        const int issueId = 123;
        const string body = "This is the issue body with some content.";
        const string englishSummary = "Summary of the issue content.";
        const string czechSummary = "Shrnutí obsahu issue.";

        // Setup summarization to succeed with English summary
        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = englishSummary,
                Provider = "OpenAI",
                Model = "gpt-4"
            });

        // Setup primary translator (DeepL) to FAIL
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = false,
                Error = "DeepL API key not configured"
            });

        // Setup fallback translator (Cohere) to succeed with Czech
        _fallbackTranslatorMock
            .Setup(x => x.TranslateToCzechAsync(englishSummary, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationResult
            {
                Success = true,
                Translation = czechSummary,
                Provider = "Cohere",
                Model = "command-a-translate"
            });

        // Capture what the notifier receives
        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        // Create service with fallback translator
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
            _fallbackTranslatorMock.Object);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert - Fallback translator should have been called
        _fallbackTranslatorMock.Verify(
            x => x.TranslateToCzechAsync(englishSummary, It.IsAny<CancellationToken>()),
            Times.Once,
            "Fallback translator should be called when primary fails");

        // Assert - Summary notifier should have received Czech summary
        Assert.NotNull(capturedNotification);
        Assert.Equal(issueId, capturedNotification.IssueId);
        Assert.Equal(czechSummary, capturedNotification.Summary);
        Assert.Equal("cs", capturedNotification.Language);
        Assert.DoesNotContain("Summary", capturedNotification.Summary); // Should NOT be English
        Assert.Contains("Shrnutí", capturedNotification.Summary); // Should be Czech
    }

    /// <summary>
    /// Verifies that when NO fallback translator is provided and DeepL fails,
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
    /// Verifies that when primary translator succeeds, fallback is NOT called.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenPrimarySucceeds_DoesNotUseFallback()
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
                Provider = "DeepL"
            });

        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
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
            _fallbackTranslatorMock.Object);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert - Fallback should NOT be called
        _fallbackTranslatorMock.Verify(
            x => x.TranslateToCzechAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Fallback should not be called when primary succeeds");
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
            _fallbackTranslatorMock.Object);

        // Act
        await service.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert - No translation should be attempted
        _primaryTranslatorMock.Verify(
            x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fallbackTranslatorMock.Verify(
            x => x.TranslateToCzechAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
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

        // Setup primary translator (DeepL) to FAIL
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "DeepL API key not configured" });

        // Setup fallback translator (Cohere) to ALSO FAIL
        _fallbackTranslatorMock
            .Setup(x => x.TranslateToCzechAsync(englishSummary, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationResult { Success = false, Error = "Cohere API error" });

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
            _fallbackTranslatorMock.Object);

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

    private static GitHubDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GitHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GitHubDbContext(options);
    }
}
