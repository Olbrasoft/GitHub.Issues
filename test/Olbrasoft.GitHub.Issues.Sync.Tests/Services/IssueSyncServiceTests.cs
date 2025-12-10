using Moq;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class IssueSyncServiceTests
{
    [Fact]
    public void IIssueSyncService_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IIssueSyncService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IssueSyncService_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IIssueSyncService).IsAssignableFrom(typeof(IssueSyncService)));
    }

    [Fact]
    public async Task IIssueSyncService_SyncIssuesAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IIssueSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        mock.Setup(x => x.SyncIssuesAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncIssuesAsync(repository, "owner", "repo", since);

        // Assert
        mock.Verify(x => x.SyncIssuesAsync(repository, "owner", "repo", since, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IIssueSyncService_SyncIssuesAsync_FullSync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IIssueSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };

        mock.Setup(x => x.SyncIssuesAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncIssuesAsync(repository, "owner", "repo");

        // Assert
        mock.Verify(x => x.SyncIssuesAsync(repository, "owner", "repo", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
