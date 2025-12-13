using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;
using Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Webhooks.Handlers;

public class IssueEventHandlerTests
{
    private readonly Mock<IRepositorySyncBusinessService> _repositoryServiceMock;
    private readonly Mock<IIssueSyncBusinessService> _issueServiceMock;
    private readonly Mock<IIssueEmbeddingGenerator> _embeddingGeneratorMock;
    private readonly Mock<IIssueUpdateNotifier> _updateNotifierMock;
    private readonly Mock<ILogger<IssueEventHandler>> _loggerMock;

    public IssueEventHandlerTests()
    {
        _repositoryServiceMock = new Mock<IRepositorySyncBusinessService>();
        _issueServiceMock = new Mock<IIssueSyncBusinessService>();
        _embeddingGeneratorMock = new Mock<IIssueEmbeddingGenerator>();
        _updateNotifierMock = new Mock<IIssueUpdateNotifier>();
        _loggerMock = new Mock<ILogger<IssueEventHandler>>();
    }

    private IssueEventHandler CreateHandler()
    {
        return new IssueEventHandler(
            _repositoryServiceMock.Object,
            _issueServiceMock.Object,
            _embeddingGeneratorMock.Object,
            _updateNotifierMock.Object,
            _loggerMock.Object);
    }

    private static GitHubIssueWebhookPayload CreatePayload(
        string action = "opened",
        bool isPullRequest = false,
        string state = "open")
    {
        return new GitHubIssueWebhookPayload
        {
            Action = action,
            Issue = new GitHubWebhookIssue
            {
                Number = 1,
                Title = "Test Issue",
                State = state,
                Body = "Test body",
                HtmlUrl = "https://github.com/owner/repo/issues/1",
                UpdatedAt = DateTimeOffset.UtcNow,
                Labels = [],
                PullRequest = isPullRequest ? new object() : null
            },
            Repository = new GitHubWebhookRepository
            {
                Id = 123,
                FullName = "owner/repo",
                HtmlUrl = "https://github.com/owner/repo"
            }
        };
    }

    [Fact]
    public async Task HandleAsync_SkipsPullRequests()
    {
        // Arrange
        var payload = CreatePayload(isPullRequest: true);
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("pull request", result.Message);
    }

    [Fact]
    public async Task HandleAsync_CreatesRepositoryIfNotExists()
    {
        // Arrange
        var payload = CreatePayload();

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Repository?)null);

        _repositoryServiceMock
            .Setup(x => x.SaveRepositoryAsync(123, "owner/repo", "https://github.com/owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _embeddingGeneratorMock
            .Setup(x => x.GenerateEmbeddingAsync("owner", "repo", 1, "Test Issue", "Test body", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _issueServiceMock
            .Setup(x => x.SaveIssueAsync(1, 1, "Test Issue", true, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 1, Number = 1 });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        _repositoryServiceMock.Verify(
            x => x.SaveRepositoryAsync(123, "owner/repo", "https://github.com/owner/repo", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OpenedAction_CreatesIssue()
    {
        // Arrange
        var payload = CreatePayload("opened");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _embeddingGeneratorMock
            .Setup(x => x.GenerateEmbeddingAsync("owner", "repo", 1, "Test Issue", "Test body", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _issueServiceMock
            .Setup(x => x.SaveIssueAsync(1, 1, "Test Issue", true, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 1, Number = 1 });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Issue created", result.Message);
        Assert.True(result.EmbeddingGenerated);
    }

    [Fact]
    public async Task HandleAsync_EditedAction_UpdatesIssue()
    {
        // Arrange
        var payload = CreatePayload("edited");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _embeddingGeneratorMock
            .Setup(x => x.GenerateEmbeddingAsync("owner", "repo", 1, "Test Issue", "Test body", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _issueServiceMock
            .Setup(x => x.SaveIssueAsync(1, 1, "Test Issue", true, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 1, Number = 1 });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Issue updated", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ClosedAction_ChangesState()
    {
        // Arrange
        var payload = CreatePayload("closed", state: "closed");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _issueServiceMock
            .Setup(x => x.GetIssueAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 1, Number = 1, Embedding = new float[] { 0.1f } });

        _issueServiceMock
            .Setup(x => x.SaveIssueAsync(1, 1, "Test Issue", false, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 1, Number = 1 });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Issue closed", result.Message);
        Assert.False(result.EmbeddingGenerated);
    }

    [Fact]
    public async Task HandleAsync_UnknownAction_IgnoresEvent()
    {
        // Arrange
        var payload = CreatePayload("unknown_action");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Ignored action", result.Message);
    }

    [Fact]
    public async Task HandleAsync_EmbeddingGenerationFails_ReturnsFailure()
    {
        // Arrange
        var payload = CreatePayload("opened");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _embeddingGeneratorMock
            .Setup(x => x.GenerateEmbeddingAsync("owner", "repo", 1, "Test Issue", "Test body", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Embedding generation failed", result.Message);
    }

    [Fact]
    public async Task HandleAsync_DeletedAction_MarksIssueAsDeleted()
    {
        // Arrange
        var payload = CreatePayload("deleted");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _issueServiceMock
            .Setup(x => x.GetIssueAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 42, Number = 1, Title = "Test Issue" });

        _issueServiceMock
            .Setup(x => x.MarkIssuesAsDeletedAsync(1, It.Is<IEnumerable<int>>(ids => ids.Contains(42)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Issue deleted", result.Message);
        Assert.Equal(1, result.IssueNumber);
        Assert.False(result.EmbeddingGenerated);

        _issueServiceMock.Verify(
            x => x.MarkIssuesAsDeletedAsync(1, It.Is<IEnumerable<int>>(ids => ids.Contains(42)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeletedAction_IssueNotInDatabase_ReturnsSuccess()
    {
        // Arrange
        var payload = CreatePayload("deleted");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _issueServiceMock
            .Setup(x => x.GetIssueAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("not found", result.Message);

        _issueServiceMock.Verify(
            x => x.MarkIssuesAsDeletedAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DeletedAction_IssueAlreadyDeleted_ReturnsSuccess()
    {
        // Arrange
        var payload = CreatePayload("deleted");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        _issueServiceMock
            .Setup(x => x.GetIssueAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue { Id = 42, Number = 1, Title = "Test Issue", IsDeleted = true });

        _issueServiceMock
            .Setup(x => x.MarkIssuesAsDeletedAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // Already deleted, no rows affected

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Issue was already deleted", result.Message);
    }
}
