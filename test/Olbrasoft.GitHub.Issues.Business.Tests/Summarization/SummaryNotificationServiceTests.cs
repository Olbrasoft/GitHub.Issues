using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Xunit;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Summarization;

public class SummaryNotificationServiceTests
{
    private readonly Mock<ISummaryNotifier> _mockNotifier;
    private readonly Mock<ILogger<SummaryNotificationService>> _mockLogger;
    private readonly SummaryNotificationService _service;

    public SummaryNotificationServiceTests()
    {
        _mockNotifier = new Mock<ISummaryNotifier>();
        _mockLogger = new Mock<ILogger<SummaryNotificationService>>();

        _service = new SummaryNotificationService(
            _mockNotifier.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task NotifySummaryAsync_WhenSuccessful_CallsNotifier()
    {
        // Arrange
        const int issueId = 123;
        const string summary = "Test summary";
        const string provider = "TestProvider";
        const string language = "en";

        // Act
        await _service.NotifySummaryAsync(issueId, summary, provider, language);

        // Assert
        _mockNotifier.Verify(n => n.NotifySummaryReadyAsync(
            It.Is<SummaryNotificationDto>(dto =>
                dto.IssueId == issueId &&
                dto.Summary == summary &&
                dto.Provider == provider &&
                dto.Language == language),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotifySummaryAsync_WhenSummaryEmpty_DoesNotCallNotifier(string? summary)
    {
        // Arrange
        const int issueId = 123;
        const string provider = "TestProvider";
        const string language = "en";

        // Act
        await _service.NotifySummaryAsync(issueId, summary!, provider, language);

        // Assert
        _mockNotifier.Verify(n => n.NotifySummaryReadyAsync(
            It.IsAny<SummaryNotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifySummaryAsync_PassesCancellationToken()
    {
        // Arrange
        const int issueId = 123;
        const string summary = "Test summary";
        const string provider = "TestProvider";
        const string language = "cs";
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _service.NotifySummaryAsync(issueId, summary, provider, language, token);

        // Assert
        _mockNotifier.Verify(n => n.NotifySummaryReadyAsync(
            It.IsAny<SummaryNotificationDto>(),
            token), Times.Once);
    }

    [Fact]
    public async Task NotifySummaryAsync_WithCzechLanguage_CallsNotifierWithCorrectLanguage()
    {
        // Arrange
        const int issueId = 456;
        const string summary = "Český souhrn";
        const string provider = "Cohere";
        const string language = "cs";

        // Act
        await _service.NotifySummaryAsync(issueId, summary, provider, language);

        // Assert
        _mockNotifier.Verify(n => n.NotifySummaryReadyAsync(
            It.Is<SummaryNotificationDto>(dto => dto.Language == "cs"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySummaryAsync_WithProviderInfo_CallsNotifierWithProvider()
    {
        // Arrange
        const int issueId = 789;
        const string summary = "Summary";
        const string provider = "Cerebras/llama3.1-8b";
        const string language = "en";

        // Act
        await _service.NotifySummaryAsync(issueId, summary, provider, language);

        // Assert
        _mockNotifier.Verify(n => n.NotifySummaryReadyAsync(
            It.Is<SummaryNotificationDto>(dto => dto.Provider == provider),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySummaryAsync_CreatesCorrectDto()
    {
        // Arrange
        const int issueId = 100;
        const string summary = "Full summary text";
        const string provider = "AI/Model";
        const string language = "en";

        SummaryNotificationDto? capturedDto = null;
        _mockNotifier.Setup(n => n.NotifySummaryReadyAsync(
                It.IsAny<SummaryNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<SummaryNotificationDto, CancellationToken>((dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);

        // Act
        await _service.NotifySummaryAsync(issueId, summary, provider, language);

        // Assert
        Assert.NotNull(capturedDto);
        Assert.Equal(issueId, capturedDto.IssueId);
        Assert.Equal(summary, capturedDto.Summary);
        Assert.Equal(provider, capturedDto.Provider);
        Assert.Equal(language, capturedDto.Language);
    }
}
