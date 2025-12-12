using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;
using Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Webhooks.Handlers;

public class LabelEventHandlerTests
{
    private readonly Mock<IRepositorySyncBusinessService> _repositoryServiceMock;
    private readonly Mock<ILabelSyncBusinessService> _labelServiceMock;
    private readonly Mock<ILogger<LabelEventHandler>> _loggerMock;

    public LabelEventHandlerTests()
    {
        _repositoryServiceMock = new Mock<IRepositorySyncBusinessService>();
        _labelServiceMock = new Mock<ILabelSyncBusinessService>();
        _loggerMock = new Mock<ILogger<LabelEventHandler>>();
    }

    private LabelEventHandler CreateHandler()
    {
        return new LabelEventHandler(
            _repositoryServiceMock.Object,
            _labelServiceMock.Object,
            _loggerMock.Object);
    }

    private static GitHubLabelWebhookPayload CreatePayload(
        string action = "created",
        GitHubLabelChanges? changes = null)
    {
        return new GitHubLabelWebhookPayload
        {
            Action = action,
            Label = new GitHubWebhookLabel
            {
                Name = "bug",
                Color = "ff0000"
            },
            Repository = new GitHubWebhookRepository
            {
                Id = 123,
                FullName = "owner/repo",
                HtmlUrl = "https://github.com/owner/repo"
            },
            Changes = changes
        };
    }

    [Fact]
    public async Task HandleAsync_RepositoryNotFound_ReturnsSuccess()
    {
        // Arrange
        var payload = CreatePayload();

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Repository?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Repository not synced", result.Message);
    }

    [Fact]
    public async Task HandleAsync_CreatedAction_SavesLabel()
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
        Assert.Contains("created", result.Message);
        _labelServiceMock.Verify(x => x.SaveLabelAsync(1, "bug", "ff0000", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EditedAction_UpdatesLabel()
    {
        // Arrange
        var changes = new GitHubLabelChanges
        {
            Name = new GitHubLabelNameChange { From = "old-name" }
        };
        var payload = CreatePayload("edited", changes);

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("updated", result.Message);
        _labelServiceMock.Verify(x => x.DeleteLabelAsync(1, "old-name", It.IsAny<CancellationToken>()), Times.Once);
        _labelServiceMock.Verify(x => x.SaveLabelAsync(1, "bug", "ff0000", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeletedAction_DeletesLabel()
    {
        // Arrange
        var payload = CreatePayload("deleted");

        _repositoryServiceMock
            .Setup(x => x.GetByFullNameAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository { Id = 1, FullName = "owner/repo" });

        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("deleted", result.Message);
        _labelServiceMock.Verify(x => x.DeleteLabelAsync(1, "bug", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownAction_IgnoresEvent()
    {
        // Arrange
        var payload = CreatePayload("unknown");

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
}
