using Moq;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Business.Search;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Business.Database;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class RepositorySyncBusinessServiceTests
{
    private readonly Mock<IMediator> _mockMediator = new();

    private RepositorySyncBusinessService CreateService()
    {
        return new RepositorySyncBusinessService(_mockMediator.Object);
    }

    [Fact]
    public async Task GetByFullNameAsync_ReturnsRepository_WhenExists()
    {
        // Arrange
        var expectedRepo = new Repository { Id = 1, FullName = "test/repo", GitHubId = 123, HtmlUrl = "url" };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<RepositoryByFullNameQuery>(q => q.FullName == "test/repo"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRepo);

        var service = CreateService();

        // Act
        var result = await service.GetByFullNameAsync("test/repo", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test/repo", result.FullName);
        Assert.Equal(123, result.GitHubId);
    }

    [Fact]
    public async Task GetByFullNameAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<RepositoryByFullNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Repository?)null);

        var service = CreateService();

        // Act
        var result = await service.GetByFullNameAsync("nonexistent/repo", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveRepositoryAsync_ReturnsSavedRepository()
    {
        // Arrange
        var expectedRepo = new Repository { Id = 1, GitHubId = 123, FullName = "owner/repo", HtmlUrl = "https://github.com/owner/repo" };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<RepositorySaveCommand>(c =>
            c.GitHubId == 123 && c.FullName == "owner/repo" && c.HtmlUrl == "https://github.com/owner/repo"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRepo);

        var service = CreateService();

        // Act
        var result = await service.SaveRepositoryAsync(123, "owner/repo", "https://github.com/owner/repo", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("owner/repo", result.FullName);
    }

    [Fact]
    public async Task UpdateLastSyncedAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        var lastSyncedAt = DateTimeOffset.UtcNow;
        _mockMediator.Setup(m => m.MediateAsync(It.Is<RepositoryUpdateLastSyncedCommand>(c =>
            c.RepositoryId == 1 && c.LastSyncedAt == lastSyncedAt),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.UpdateLastSyncedAsync(1, lastSyncedAt, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateLastSyncedAsync_ReturnsFalse_WhenRepositoryNotFound()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<RepositoryUpdateLastSyncedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var result = await service.UpdateLastSyncedAsync(999, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetLastSyncedAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.Is<RepositoryResetLastSyncedCommand>(c =>
            c.FullName == "test/repo"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.ResetLastSyncedAsync("test/repo", CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResetLastSyncedAsync_ReturnsFalse_WhenRepositoryNotFound()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<RepositoryResetLastSyncedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var result = await service.ResetLastSyncedAsync("nonexistent/repo", CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
