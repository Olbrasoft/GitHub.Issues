using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Comprehensive unit tests for SummaryCacheService.
/// Covers all methods and scenarios for 80%+ code coverage.
/// </summary>
public class SummaryCacheServiceTests
{
    private readonly Mock<ICachedTextRepository> _repositoryMock;
    private readonly Mock<ILogger<SummaryCacheService>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SummaryCacheService _sut;

    public SummaryCacheServiceTests()
    {
        _repositoryMock = new Mock<ICachedTextRepository>();
        _loggerMock = new Mock<ILogger<SummaryCacheService>>();
        _timeProvider = new FakeTimeProvider();
        _sut = new SummaryCacheService(_repositoryMock.Object, _timeProvider, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenRepositoryIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SummaryCacheService(null!, _timeProvider, _loggerMock.Object));

        Assert.Equal("cachedTextRepository", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SummaryCacheService(_repositoryMock.Object, null!, _loggerMock.Object));

        Assert.Equal("timeProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SummaryCacheService(_repositoryMock.Object, _timeProvider, null!));

        Assert.Equal("logger", exception.ParamName);
    }

    #endregion

    #region GetCachedSummaryAsync Tests

    [Fact]
    public async Task GetCachedSummaryAsync_WhenCachedNotFound_ReturnsNull()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        var issueUpdatedAt = DateTime.UtcNow;

        _repositoryMock.Setup(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedText?)null);

        // Act
        var result = await _sut.GetCachedSummaryAsync(issueId, languageId, textTypeId, issueUpdatedAt);

        // Assert
        Assert.Null(result);
        _repositoryMock.Verify(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCachedSummaryAsync_WhenCachedFoundAndFresh_ReturnsContent()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        const string expectedContent = "Cached summary content";

        var issueUpdatedAt = new DateTime(2025, 12, 20, 10, 0, 0, DateTimeKind.Utc);
        var cachedAt = new DateTime(2025, 12, 20, 11, 0, 0, DateTimeKind.Utc); // Cached AFTER issue update (fresh)

        var cachedText = new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = textTypeId,
            Content = expectedContent,
            CachedAt = cachedAt
        };

        _repositoryMock.Setup(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedText);

        // Act
        var result = await _sut.GetCachedSummaryAsync(issueId, languageId, textTypeId, issueUpdatedAt);

        // Assert
        Assert.Equal(expectedContent, result);
        _repositoryMock.Verify(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify logging for cache hit
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cache HIT")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCachedSummaryAsync_WhenCachedFoundButStale_DeletesCacheAndReturnsNull()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        const string cachedContent = "Stale cached summary";

        var issueUpdatedAt = new DateTime(2025, 12, 20, 12, 0, 0, DateTimeKind.Utc); // Issue updated later
        var cachedAt = new DateTime(2025, 12, 20, 11, 0, 0, DateTimeKind.Utc); // Cached BEFORE issue update (stale)

        var cachedText = new CachedText
        {
            IssueId = issueId,
            LanguageId = languageId,
            TextTypeId = textTypeId,
            Content = cachedContent,
            CachedAt = cachedAt
        };

        _repositoryMock.Setup(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedText);

        // Act
        var result = await _sut.GetCachedSummaryAsync(issueId, languageId, textTypeId, issueUpdatedAt);

        // Assert
        Assert.Null(result);
        _repositoryMock.Verify(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.DeleteAsync(cachedText, It.IsAny<CancellationToken>()), Times.Once);

        // Verify logging for stale cache
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cache STALE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCachedSummaryAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        var issueUpdatedAt = DateTime.UtcNow;

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _repositoryMock.Setup(r => r.GetByIssueAsync(issueId, languageId, textTypeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.GetCachedSummaryAsync(issueId, languageId, textTypeId, issueUpdatedAt, cts.Token));
    }

    #endregion

    #region SaveSummaryAsync Tests

    [Fact]
    public async Task SaveSummaryAsync_WhenCalled_SavesCachedTextWithCurrentTimestamp()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        const string content = "Summary content to cache";

        var expectedTimestamp = new DateTime(2025, 12, 20, 15, 30, 0, DateTimeKind.Utc);
        _timeProvider.SetUtcNow(new DateTimeOffset(expectedTimestamp));

        CachedText? capturedCachedText = null;
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()))
            .Callback<CachedText, CancellationToken>((ct, _) => capturedCachedText = ct)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SaveSummaryAsync(issueId, languageId, textTypeId, content);

        // Assert
        Assert.NotNull(capturedCachedText);
        Assert.Equal(issueId, capturedCachedText.IssueId);
        Assert.Equal(languageId, capturedCachedText.LanguageId);
        Assert.Equal(textTypeId, capturedCachedText.TextTypeId);
        Assert.Equal(content, capturedCachedText.Content);
        Assert.Equal(expectedTimestamp, capturedCachedText.CachedAt);

        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveSummaryAsync_WhenCalled_LogsDebugMessage()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        const string content = "Summary content";

        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SaveSummaryAsync(issueId, languageId, textTypeId, content);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Saved to cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveSummaryAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        // Arrange
        const int issueId = 1;
        const int languageId = 1;
        const int textTypeId = 1;
        const string content = "Summary content";

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<CachedText>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.SaveSummaryAsync(issueId, languageId, textTypeId, content, cts.Token));
    }

    #endregion
}
