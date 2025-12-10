using Moq;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class GitHubSyncServiceTests
{
    [Fact]
    public void IGitHubSyncService_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IGitHubSyncService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void GitHubSyncService_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IGitHubSyncService).IsAssignableFrom(typeof(GitHubSyncService)));
    }

    [Fact]
    public async Task IGitHubSyncService_SyncAllRepositoriesAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();

        mock.Setup(x => x.SyncAllRepositoriesAsync(
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncAllRepositoriesAsync();

        // Assert
        mock.Verify(x => x.SyncAllRepositoriesAsync(null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncAllRepositoriesAsync_SmartMode_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();

        mock.Setup(x => x.SyncAllRepositoriesAsync(null, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncAllRepositoriesAsync(null, true);

        // Assert
        mock.Verify(x => x.SyncAllRepositoriesAsync(null, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncRepositoriesAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();
        var repos = new[] { "owner/repo1", "owner/repo2" };

        mock.Setup(x => x.SyncRepositoriesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncRepositoriesAsync(repos);

        // Assert
        mock.Verify(x => x.SyncRepositoriesAsync(repos, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncRepositoryAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        mock.Setup(x => x.SyncRepositoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncRepositoryAsync("owner", "repo", since);

        // Assert
        mock.Verify(x => x.SyncRepositoryAsync("owner", "repo", since, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
