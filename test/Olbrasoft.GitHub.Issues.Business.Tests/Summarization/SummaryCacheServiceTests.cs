using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;
using Xunit;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Summarization;

public class SummaryCacheServiceTests
{
    private readonly Mock<ICachedTextRepository> _mockRepository;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<ILogger<SummaryCacheService>> _mockLogger;
    private readonly SummaryCacheService _service;
    private readonly DateTime _testTime;

    public SummaryCacheServiceTests()
    {
        _mockRepository = new Mock<ICachedTextRepository>();
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockLogger = new Mock<ILogger<SummaryCacheService>>();

        _testTime = new DateTime(2025, 12, 29, 12, 0, 0, DateTimeKind.Utc);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_testTime));

        _service = new SummaryCacheService(
            _mockRepository.Object,
            _mockTimeProvider.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetIfFreshAsync_WhenCacheExists_ReturnsSummary()
    {
        // Arrange
        const int issueId = 123;
        const int languageCode = (int)LanguageCode.EnUS;
        var issueUpdatedAt = _testTime.AddDays(-1);
        const string expectedSummary = "Test summary";

        _mockRepository
            .Setup(r => r.GetIfFreshAsync(
                issueId,
                languageCode,
                (int)TextTypeCode.ListSummary,
                issueUpdatedAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _service.GetIfFreshAsync(issueId, languageCode, issueUpdatedAt);

        // Assert
        Assert.Equal(expectedSummary, result);
        _mockRepository.Verify(r => r.GetIfFreshAsync(
            issueId,
            languageCode,
            (int)TextTypeCode.ListSummary,
            issueUpdatedAt,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIfFreshAsync_WhenCacheDoesNotExist_ReturnsNull()
    {
        // Arrange
        const int issueId = 123;
        const int languageCode = (int)LanguageCode.CsCZ;
        var issueUpdatedAt = _testTime.AddDays(-1);

        _mockRepository
            .Setup(r => r.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.GetIfFreshAsync(issueId, languageCode, issueUpdatedAt);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_SavesSummaryWithCorrectParameters()
    {
        // Arrange
        const int issueId = 456;
        const int languageCode = (int)LanguageCode.EnUS;
        const string summary = "New summary";
        CachedText? capturedCachedText = null;

        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()))
            .Callback<CachedText, CancellationToken>((ct, _) => capturedCachedText = ct)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SaveAsync(issueId, languageCode, summary);

        // Assert
        Assert.NotNull(capturedCachedText);
        Assert.Equal(issueId, capturedCachedText.IssueId);
        Assert.Equal(languageCode, capturedCachedText.LanguageId);
        Assert.Equal((int)TextTypeCode.ListSummary, capturedCachedText.TextTypeId);
        Assert.Equal(summary, capturedCachedText.Content);
        Assert.Equal(_testTime, capturedCachedText.CachedAt);

        _mockRepository.Verify(r => r.SaveAsync(
            It.IsAny<CachedText>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_UsesCzechLanguageCode()
    {
        // Arrange
        const int issueId = 789;
        const int languageCode = (int)LanguageCode.CsCZ;
        const string summary = "Czech summary";
        CachedText? capturedCachedText = null;

        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()))
            .Callback<CachedText, CancellationToken>((ct, _) => capturedCachedText = ct)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SaveAsync(issueId, languageCode, summary);

        // Assert
        Assert.NotNull(capturedCachedText);
        Assert.Equal((int)LanguageCode.CsCZ, capturedCachedText.LanguageId);
    }

    [Fact]
    public async Task GetIfFreshAsync_PassesCancellationToken()
    {
        // Arrange
        const int issueId = 999;
        const int languageCode = (int)LanguageCode.EnUS;
        var issueUpdatedAt = _testTime;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockRepository
            .Setup(r => r.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                token))
            .ReturnsAsync("summary");

        // Act
        await _service.GetIfFreshAsync(issueId, languageCode, issueUpdatedAt, token);

        // Assert
        _mockRepository.Verify(r => r.GetIfFreshAsync(
            issueId,
            languageCode,
            (int)TextTypeCode.ListSummary,
            issueUpdatedAt,
            token), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_PassesCancellationToken()
    {
        // Arrange
        const int issueId = 888;
        const int languageCode = (int)LanguageCode.CsCZ;
        const string summary = "Test";
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _service.SaveAsync(issueId, languageCode, summary, token);

        // Assert
        _mockRepository.Verify(r => r.SaveAsync(
            It.IsAny<CachedText>(),
            token), Times.Once);
    }
}
