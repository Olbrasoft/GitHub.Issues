using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.RepositoryQueryHandlers;

public class RepositoriesSyncStatusQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsAllRepositories()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.AddRange(
            new Repository { FullName = "owner/repo1", GitHubId = 1, HtmlUrl = "url1" },
            new Repository { FullName = "owner/repo2", GitHubId = 2, HtmlUrl = "url2" },
            new Repository { FullName = "owner/repo3", GitHubId = 3, HtmlUrl = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenNoRepositories()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsResultsOrderedByFullName()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.AddRange(
            new Repository { FullName = "zebra/repo", GitHubId = 1, HtmlUrl = "url1" },
            new Repository { FullName = "alpha/repo", GitHubId = 2, HtmlUrl = "url2" },
            new Repository { FullName = "beta/repo", GitHubId = 3, HtmlUrl = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal("alpha/repo", result[0].FullName);
        Assert.Equal("beta/repo", result[1].FullName);
        Assert.Equal("zebra/repo", result[2].FullName);
    }

    [Fact]
    public async Task HandleAsync_IncludesLastSyncedAt()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var syncTime = DateTimeOffset.UtcNow.AddHours(-2);
        context.Repositories.AddRange(
            new Repository { FullName = "synced/repo", GitHubId = 1, HtmlUrl = "url1", LastSyncedAt = syncTime },
            new Repository { FullName = "never-synced/repo", GitHubId = 2, HtmlUrl = "url2", LastSyncedAt = null }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        var synced = result.First(r => r.FullName == "synced/repo");
        var neverSynced = result.First(r => r.FullName == "never-synced/repo");

        Assert.NotNull(synced.LastSyncedAt);
        Assert.Null(neverSynced.LastSyncedAt);
    }

    [Fact]
    public async Task HandleAsync_IncludesIssueCount()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "with-issues/repo", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "no-issues/repo", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        // Add issues to repo1
        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 1, Title = "Issue 1", Embedding = new float[] { 1.0f }, Url = "url1" },
            new Issue { RepositoryId = repo1.Id, Number = 2, Title = "Issue 2", Embedding = new float[] { 1.0f }, Url = "url2" },
            new Issue { RepositoryId = repo1.Id, Number = 3, Title = "Issue 3", Embedding = new float[] { 1.0f }, Url = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        var withIssues = result.First(r => r.FullName == "with-issues/repo");
        var noIssues = result.First(r => r.FullName == "no-issues/repo");

        Assert.Equal(3, withIssues.IssueCount);
        Assert.Equal(0, noIssues.IssueCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCorrectDtoFields()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var syncTime = DateTimeOffset.UtcNow;
        var repo = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = syncTime
        };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        // Add some issues
        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", Embedding = new float[] { 1.0f }, Url = "url1" },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Issue 2", Embedding = new float[] { 1.0f }, Url = "url2" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSyncStatusQueryHandler(context);
        var query = new RepositoriesSyncStatusQuery(_mockProcessor.Object);

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).Single();

        // Assert
        Assert.Equal(repo.Id, result.Id);
        Assert.Equal("test/repo", result.FullName);
        Assert.Equal(2, result.IssueCount);
        Assert.NotNull(result.LastSyncedAt);
    }
}
