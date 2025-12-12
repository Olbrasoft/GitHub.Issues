using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class IssueSyncBusinessServiceTests
{
    private readonly Mock<IMediator> _mockMediator = new();

    private IssueSyncBusinessService CreateService()
    {
        return new IssueSyncBusinessService(_mockMediator.Object);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsIssue_WhenExists()
    {
        // Arrange
        var expectedIssue = new Issue
        {
            Id = 1,
            RepositoryId = 1,
            Number = 42,
            Title = "Test Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = []
        };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueByRepositoryAndNumberQuery>(q =>
            q.RepositoryId == 1 && q.Number == 42),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIssue);

        var service = CreateService();

        // Act
        var result = await service.GetIssueAsync(1, 42, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.Number);
        Assert.Equal("Test Issue", result.Title);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueByRepositoryAndNumberQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        var service = CreateService();

        // Act
        var result = await service.GetIssueAsync(1, 999, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIssuesByRepositoryAsync_ReturnsDictionary()
    {
        // Arrange
        var issues = new Dictionary<int, Issue>
        {
            [1] = new() { Id = 1, RepositoryId = 1, Number = 1, Title = "Issue 1", IsOpen = true, Url = "url1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = [] },
            [2] = new() { Id = 2, RepositoryId = 1, Number = 2, Title = "Issue 2", IsOpen = false, Url = "url2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = [] }
        };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssuesByRepositoryQuery>(q => q.RepositoryId == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        var service = CreateService();

        // Act
        var result = await service.GetIssuesByRepositoryAsync(1, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(1));
        Assert.True(result.ContainsKey(2));
    }

    [Fact]
    public async Task SaveIssueAsync_ReturnsSavedIssue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        var expectedIssue = new Issue
        {
            Id = 1,
            RepositoryId = 1,
            Number = 42,
            Title = "New Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = now,
            SyncedAt = now,
            Embedding = embedding
        };

        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueSaveCommand>(c =>
            c.RepositoryId == 1 &&
            c.Number == 42 &&
            c.Title == "New Issue" &&
            c.IsOpen == true &&
            c.Url == "url"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIssue);

        var service = CreateService();

        // Act
        var result = await service.SaveIssueAsync(1, 42, "New Issue", true, "url", now, now, embedding, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(42, result.Number);
    }

    [Fact]
    public async Task UpdateEmbeddingAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueUpdateEmbeddingCommand>(c =>
            c.IssueId == 1 && c.Embedding == embedding),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.UpdateEmbeddingAsync(1, embedding, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateEmbeddingAsync_ReturnsFalse_WhenIssueNotFound()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueUpdateEmbeddingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var result = await service.UpdateEmbeddingAsync(999, [0.1f], CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BatchSetParentsAsync_ReturnsUpdatedCount()
    {
        // Arrange
        var childToParentMap = new Dictionary<int, int?>
        {
            [1] = 10,
            [2] = 10,
            [3] = null
        };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueBatchSetParentsCommand>(c =>
            c.ChildToParentMap.Count == 3),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = CreateService();

        // Act
        var result = await service.BatchSetParentsAsync(childToParentMap, CancellationToken.None);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task SyncLabelsAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        var labelNames = new List<string> { "bug", "urgent" };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueSyncLabelsCommand>(c =>
            c.IssueId == 1 && c.RepositoryId == 1 && c.LabelNames.Count == 2),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.SyncLabelsAsync(1, 1, labelNames, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SyncLabelsAsync_ReturnsFalse_WhenFailed()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueSyncLabelsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var result = await service.SyncLabelsAsync(999, 1, ["bug"], CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
