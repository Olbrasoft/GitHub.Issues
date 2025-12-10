using Moq;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class RepositorySyncServiceTests
{
    [Fact]
    public void IRepositorySyncService_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IRepositorySyncService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void RepositorySyncService_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IRepositorySyncService).IsAssignableFrom(typeof(RepositorySyncService)));
    }

    [Fact]
    public async Task IRepositorySyncService_EnsureRepositoryAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IRepositorySyncService>();
        var expectedRepo = new Repository { Id = 1, FullName = "owner/repo", GitHubId = 123 };

        mock.Setup(x => x.EnsureRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRepo);

        // Act
        var result = await mock.Object.EnsureRepositoryAsync("owner", "repo");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("owner/repo", result.FullName);
        Assert.Equal(123, result.GitHubId);
    }

    [Fact]
    public async Task IRepositorySyncService_FetchAllRepositoriesForOwnerAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IRepositorySyncService>();
        var repositories = new List<string> { "owner/repo1", "owner/repo2", "owner/repo3" };

        mock.Setup(x => x.FetchAllRepositoriesForOwnerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);

        // Act
        var result = await mock.Object.FetchAllRepositoriesForOwnerAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("owner/repo1", result);
        Assert.Contains("owner/repo2", result);
    }
}
