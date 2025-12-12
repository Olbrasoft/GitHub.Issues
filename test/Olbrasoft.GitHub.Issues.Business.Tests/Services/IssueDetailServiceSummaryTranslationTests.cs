using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Tests for IssueSummaryService summary translation.
/// These tests verify the summarization + translation flow with primary translator and fallback behavior.
/// </summary>
public class IssueSummaryServiceTests
{
    private readonly Mock<ISummarizationService> _summarizationServiceMock;
    private readonly Mock<ITranslationFallbackService> _translationServiceMock;
    private readonly Mock<ISummaryNotifier> _summaryNotifierMock;
    private readonly Mock<ILogger<IssueSummaryService>> _loggerMock;

    public IssueSummaryServiceTests()
    {
        _summarizationServiceMock = new Mock<ISummarizationService>();
        _translationServiceMock = new Mock<ITranslationFallbackService>();
        _summaryNotifierMock = new Mock<ISummaryNotifier>();
        _loggerMock = new Mock<ILogger<IssueSummaryService>>();
    }

    private IssueSummaryService CreateService()
    {
        return new IssueSummaryService(
            _summarizationServiceMock.Object,
            _translationServiceMock.Object,
            _summaryNotifierMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that when translation fails, English summary is used as fallback with "(EN fallback)" marker.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryAsync_WhenTranslationFails_SendsEnglishAsFallback()
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

        _translationServiceMock
            .Setup(x => x.TranslateWithFallbackAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(false, null, null, false, "No API key"));

        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(issueId, body, "cs");

        // Assert - Should send English as fallback
        Assert.NotNull(capturedNotification);
        Assert.Equal(englishSummary, capturedNotification.Summary);
        Assert.Equal("en", capturedNotification.Language); // Falls back to English
        Assert.Contains("EN fallback", capturedNotification.Provider ?? "");
    }

    /// <summary>
    /// Verifies that when translation succeeds, Czech summary is sent.
    /// </summary>
    [Fact]
    public async Task GenerateSummaryAsync_WhenTranslationSucceeds_SendsCzechSummary()
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

        _translationServiceMock
            .Setup(x => x.TranslateWithFallbackAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(true, czechSummary, "Azure", false, null));

        SummaryNotificationDto? capturedNotification = null;
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedNotification = dto)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(issueId, body, "cs");

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
    public async Task GenerateSummaryAsync_WhenLanguageIsEnglish_DoesNotTranslate()
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

        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(issueId, body, "en");

        // Assert - No translation should be attempted
        _translationServiceMock.Verify(
            x => x.TranslateWithFallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should receive English summary
        Assert.NotNull(capturedNotification);
        Assert.Equal(englishSummary, capturedNotification.Summary);
        Assert.Equal("en", capturedNotification.Language);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenBothModeAndTranslationFails_SendsEnglishAsCzechFallback()
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

        // Setup translation to FAIL
        _translationServiceMock
            .Setup(x => x.TranslateWithFallbackAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(false, null, null, false, "API key not configured"));

        // Capture notifications
        var notifications = new List<SummaryNotificationDto>();
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act - Use "both" language mode
        await service.GenerateSummaryAsync(issueId, body, "both");

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
    public async Task GenerateSummaryAsync_WhenBothModeAndTranslationSucceeds_SendsBothLanguages()
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

        _translationServiceMock
            .Setup(x => x.TranslateWithFallbackAsync(englishSummary, "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(true, czechSummary, "Azure", false, null));

        var notifications = new List<SummaryNotificationDto>();
        _summaryNotifierMock
            .Setup(x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(issueId, body, "both");

        // Assert - Should have 2 notifications
        Assert.Equal(2, notifications.Count);

        // First: English
        Assert.Equal(englishSummary, notifications[0].Summary);
        Assert.Equal("en", notifications[0].Language);

        // Second: Czech
        Assert.Equal(czechSummary, notifications[1].Summary);
        Assert.Equal("cs", notifications[1].Language);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenBodyIsEmpty_DoesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(1, "", "both");

        // Assert
        _summarizationServiceMock.Verify(
            x => x.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _summaryNotifierMock.Verify(
            x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenSummarizationFails_DoesNotSendNotification()
    {
        // Arrange
        _summarizationServiceMock
            .Setup(x => x.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = false,
                Error = "Service unavailable"
            });

        var service = CreateService();

        // Act
        await service.GenerateSummaryAsync(1, "Some body text", "both");

        // Assert
        _summaryNotifierMock.Verify(
            x => x.NotifySummaryReadyAsync(It.IsAny<SummaryNotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
