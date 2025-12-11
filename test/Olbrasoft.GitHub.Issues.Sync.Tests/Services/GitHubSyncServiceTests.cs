using Moq;
using Olbrasoft.GitHub.Issues.Data.Dtos;
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
        var expectedStats = new SyncStatisticsDto { TotalFound = 10, Created = 5, Updated = 3, Unchanged = 2 };

        mock.Setup(x => x.SyncAllRepositoriesAsync(
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncAllRepositoriesAsync();

        // Assert
        mock.Verify(x => x.SyncAllRepositoriesAsync(null, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(10, result.TotalFound);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncAllRepositoriesAsync_SmartMode_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();
        var expectedStats = new SyncStatisticsDto { TotalFound = 5 };

        mock.Setup(x => x.SyncAllRepositoriesAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncAllRepositoriesAsync(null, true);

        // Assert
        mock.Verify(x => x.SyncAllRepositoriesAsync(null, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(5, result.TotalFound);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncRepositoriesAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();
        var repos = new[] { "owner/repo1", "owner/repo2" };
        var expectedStats = new SyncStatisticsDto { TotalFound = 20 };

        mock.Setup(x => x.SyncRepositoriesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncRepositoriesAsync(repos);

        // Assert
        mock.Verify(x => x.SyncRepositoriesAsync(repos, null, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(20, result.TotalFound);
    }

    [Fact]
    public async Task IGitHubSyncService_SyncRepositoryAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubSyncService>();
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var expectedStats = new SyncStatisticsDto
        {
            TotalFound = 15,
            Created = 3,
            Updated = 7,
            Unchanged = 5,
            SinceTimestamp = since
        };

        mock.Setup(x => x.SyncRepositoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncRepositoryAsync("owner", "repo", since);

        // Assert
        mock.Verify(x => x.SyncRepositoryAsync("owner", "repo", since, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(15, result.TotalFound);
        Assert.Equal(since, result.SinceTimestamp);
    }
}
