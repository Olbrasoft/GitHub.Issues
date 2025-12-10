using Moq;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class EventSyncServiceTests
{
    [Fact]
    public void IEventSyncService_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IEventSyncService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void EventSyncService_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IEventSyncService).IsAssignableFrom(typeof(EventSyncService)));
    }

    [Fact]
    public async Task IEventSyncService_SyncEventsAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IEventSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        mock.Setup(x => x.SyncEventsAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncEventsAsync(repository, "owner", "repo", since);

        // Assert
        mock.Verify(x => x.SyncEventsAsync(repository, "owner", "repo", since, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IEventSyncService_SyncEventsAsync_WithoutSince_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IEventSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };

        mock.Setup(x => x.SyncEventsAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncEventsAsync(repository, "owner", "repo");

        // Assert
        mock.Verify(x => x.SyncEventsAsync(repository, "owner", "repo", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
