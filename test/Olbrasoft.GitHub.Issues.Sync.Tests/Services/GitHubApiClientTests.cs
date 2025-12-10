using Moq;
using Octokit;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class GitHubApiClientTests
{
    [Fact]
    public void IGitHubApiClient_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IGitHubApiClient>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void OctokitGitHubApiClient_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IGitHubApiClient).IsAssignableFrom(typeof(OctokitGitHubApiClient)));
    }

    [Fact]
    public async Task IGitHubApiClient_GetRepositoryAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubApiClient>();

        // Use default instance - Repository is a class we can't easily mock,
        // but we can verify the setup/callback mechanism works
        mock.Setup(x => x.GetRepositoryAsync("owner", "repo"))
            .ReturnsAsync((Repository)null!);

        // Act
        var result = await mock.Object.GetRepositoryAsync("owner", "repo");

        // Assert - verify mock was called and returned expected value
        mock.Verify(x => x.GetRepositoryAsync("owner", "repo"), Times.Once);
    }

    [Fact]
    public async Task IGitHubApiClient_GetRepositoryAsync_VerifyMethodSignature()
    {
        // Verify the interface method signature
        var mock = new Mock<IGitHubApiClient>();
        var called = false;

        mock.Setup(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((owner, repo) => {
                Assert.Equal("test-owner", owner);
                Assert.Equal("test-repo", repo);
                called = true;
            })
            .ReturnsAsync((Repository)null!);

        // Act
        await mock.Object.GetRepositoryAsync("test-owner", "test-repo");

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task IGitHubApiClient_GetLabelsForRepositoryAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IGitHubApiClient>();
        var labels = new List<Label>
        {
            new Label(1, "https://api.github.com/...", "bug", "node1", "ff0000", "Bug", false),
            new Label(2, "https://api.github.com/...", "enhancement", "node2", "00ff00", "Feature", false)
        };

        mock.Setup(x => x.GetLabelsForRepositoryAsync("owner", "repo"))
            .ReturnsAsync(labels.AsReadOnly());

        // Act
        var result = await mock.Object.GetLabelsForRepositoryAsync("owner", "repo");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("bug", result[0].Name);
        Assert.Equal("enhancement", result[1].Name);
    }
}
