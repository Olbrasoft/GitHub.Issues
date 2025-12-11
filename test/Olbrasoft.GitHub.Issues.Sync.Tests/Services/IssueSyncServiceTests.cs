using Moq;
using Olbrasoft.GitHub.Issues.Data.Dtos;
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
        var expectedStats = new SyncStatisticsDto
        {
            TotalFound = 10,
            Created = 3,
            Updated = 4,
            Unchanged = 3,
            SinceTimestamp = since
        };

        mock.Setup(x => x.SyncIssuesAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncIssuesAsync(repository, "owner", "repo", since);

        // Assert
        mock.Verify(x => x.SyncIssuesAsync(repository, "owner", "repo", since, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(10, result.TotalFound);
        Assert.Equal(since, result.SinceTimestamp);
    }

    [Fact]
    public async Task IIssueSyncService_SyncIssuesAsync_FullSync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IIssueSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };
        var expectedStats = new SyncStatisticsDto
        {
            TotalFound = 50,
            Created = 50,
            Updated = 0,
            Unchanged = 0,
            SinceTimestamp = null
        };

        mock.Setup(x => x.SyncIssuesAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await mock.Object.SyncIssuesAsync(repository, "owner", "repo");

        // Assert
        mock.Verify(x => x.SyncIssuesAsync(repository, "owner", "repo", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(50, result.TotalFound);
        Assert.Null(result.SinceTimestamp);
    }
}
