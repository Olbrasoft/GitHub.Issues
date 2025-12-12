using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;
using Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Webhooks.Handlers;

public class RepositoryEventHandlerTests
{
    private readonly Mock<IRepositorySyncBusinessService> _repositoryServiceMock;
    private readonly Mock<ILogger<RepositoryEventHandler>> _loggerMock;

    public RepositoryEventHandlerTests()
    {
        _repositoryServiceMock = new Mock<IRepositorySyncBusinessService>();
        _loggerMock = new Mock<ILogger<RepositoryEventHandler>>();
    }

    private RepositoryEventHandler CreateHandler()
    {
        return new RepositoryEventHandler(
            _repositoryServiceMock.Object,
            _loggerMock.Object);
    }

    private static GitHubRepositoryWebhookPayload CreatePayload(string action = "created")
    {
        return new GitHubRepositoryWebhookPayload
        {
            Action = action,
            Repository = new GitHubWebhookRepository
            {
                Id = 123,
                FullName = "owner/repo",
                HtmlUrl = "https://github.com/owner/repo"
            }
        };
    }

    [Fact]
    public async Task HandleAsync_NonCreatedAction_IgnoresEvent()
    {
        // Arrange
        var payload = CreatePayload("deleted");
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Ignored action", result.Message);
    }

    [Fact]
    public async Task HandleAsync_RepositoryAlreadyExists_ReturnsSuccess()
    {
        // Arrange
        var payload = CreatePayload("created");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("already exists", result.Message);
    }

    [Fact]
    public async Task HandleAsync_NewRepository_AutoDiscovers()
    {
        // Arrange
        var payload = CreatePayload("created");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Repository?)null);

        _repositoryServiceMock
            .Setup(x => x.SaveRepositoryAsync(123, "owner/repo", "https://github.com/owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("auto-discovered", result.Message);
        _repositoryServiceMock.Verify(
            x => x.SaveRepositoryAsync(123, "owner/repo", "https://github.com/owner/repo", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Exception_ReturnsFailure()
    {
        // Arrange
        var payload = CreatePayload("created");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Database error", result.Message);
    }
}
