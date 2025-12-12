using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class IssueEmbeddingGeneratorTests
{
    private readonly Mock<IGitHubIssueApiClient> _mockApiClient;
    private readonly Mock<IEmbeddingTextBuilder> _mockTextBuilder;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<IssueEmbeddingGenerator>> _mockLogger;
    private readonly IssueEmbeddingGenerator _generator;

    public IssueEmbeddingGeneratorTests()
    {
        _mockApiClient = new Mock<IGitHubIssueApiClient>();
        _mockTextBuilder = new Mock<IEmbeddingTextBuilder>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<IssueEmbeddingGenerator>>();

        _generator = new IssueEmbeddingGenerator(
            _mockApiClient.Object,
            _mockTextBuilder.Object,
            _mockEmbeddingService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void IIssueEmbeddingGenerator_InterfaceExists()
    {
        var mock = new Mock<IIssueEmbeddingGenerator>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IssueEmbeddingGenerator_ImplementsInterface()
    {
        Assert.True(typeof(IIssueEmbeddingGenerator).IsAssignableFrom(typeof(IssueEmbeddingGenerator)));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenApiClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueEmbeddingGenerator(
            null!,
            _mockTextBuilder.Object,
            _mockEmbeddingService.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTextBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueEmbeddingGenerator(
            _mockApiClient.Object,
            null!,
            _mockEmbeddingService.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenEmbeddingServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueEmbeddingGenerator(
            _mockApiClient.Object,
            _mockTextBuilder.Object,
            null!,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueEmbeddingGenerator(
            _mockApiClient.Object,
            _mockTextBuilder.Object,
            _mockEmbeddingService.Object,
            null!));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_FetchesComments_BuildsText_GeneratesEmbedding()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        var body = "Test body";
        var labels = new List<string> { "bug", "enhancement" };
        var comments = new List<string> { "Comment 1", "Comment 2" };
        var expectedText = "Combined text for embedding";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, comments))
            .Returns(expectedText);

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                expectedText,
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedEmbedding, result);

        _mockApiClient.Verify(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()), Times.Once);
        _mockTextBuilder.Verify(x => x.CreateEmbeddingText(title, body, labels, comments), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingAsync(expectedText, EmbeddingInputType.Document, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ContinuesWithoutComments_WhenCommentFetchFails()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        var body = "Test body";
        var labels = new List<string> { "bug" };
        var expectedText = "Combined text without comments";
        var expectedEmbedding = new float[] { 0.1f, 0.2f };

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, null))
            .Returns(expectedText);

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                expectedText,
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedEmbedding, result);

        // Verify comments were attempted but text builder was called with null comments
        _mockApiClient.Verify(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()), Times.Once);
        _mockTextBuilder.Verify(x => x.CreateEmbeddingText(title, body, labels, null), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsNull_WhenEmbeddingServiceReturnsNull()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        var body = "Test body";
        var labels = new List<string>();

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, It.IsAny<IReadOnlyList<string>?>()))
            .Returns("Some text");

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsNull_WhenEmbeddingServiceThrows()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        string? body = null;
        var labels = new List<string>();

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, It.IsAny<IReadOnlyList<string>?>()))
            .Returns("Some text");

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service unavailable"));

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_HandlesNullBody()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        string? body = null;
        var labels = new List<string>();
        var expectedEmbedding = new float[] { 0.1f };

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, It.IsAny<IReadOnlyList<string>?>()))
            .Returns("Title only");

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                "Title only",
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedEmbedding, result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_HandlesEmptyLabels()
    {
        // Arrange
        var owner = "testowner";
        var repo = "testrepo";
        var issueNumber = 42;
        var title = "Test Issue";
        var body = "Test body";
        IReadOnlyList<string> labels = Array.Empty<string>();
        var expectedEmbedding = new float[] { 0.5f };

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(owner, repo, issueNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(title, body, labels, It.IsAny<IReadOnlyList<string>?>()))
            .Returns("Some text");

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                EmbeddingInputType.Document,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _generator.GenerateEmbeddingAsync(owner, repo, issueNumber, title, body, labels);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockApiClient.Setup(x => x.FetchIssueCommentsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), token))
            .ReturnsAsync(new List<string>());

        _mockTextBuilder.Setup(x => x.CreateEmbeddingText(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns("text");

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), EmbeddingInputType.Document, token))
            .ReturnsAsync(new float[] { 1.0f });

        // Act
        await _generator.GenerateEmbeddingAsync("o", "r", 1, "t", "b", new List<string>(), token);

        // Assert
        _mockApiClient.Verify(x => x.FetchIssueCommentsAsync("o", "r", 1, token), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingAsync("text", EmbeddingInputType.Document, token), Times.Once);
    }

    [Fact]
    public async Task IIssueEmbeddingGenerator_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IIssueEmbeddingGenerator>();
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        mock.Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await mock.Object.GenerateEmbeddingAsync("owner", "repo", 1, "title", "body", new List<string>());

        // Assert
        Assert.Equal(expectedEmbedding, result);
        mock.Verify(x => x.GenerateEmbeddingAsync("owner", "repo", 1, "title", "body", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
