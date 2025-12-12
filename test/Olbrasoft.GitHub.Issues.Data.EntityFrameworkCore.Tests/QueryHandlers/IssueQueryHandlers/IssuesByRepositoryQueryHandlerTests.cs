using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

public class IssuesByRepositoryQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmptyDictionary_WhenNoIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new IssuesByRepositoryQueryHandler(context);
        var query = new IssuesByRepositoryQuery(_mockProcessor.Object) { RepositoryId = repo.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsDictionaryKeyedByNumber()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Issue 2", IsOpen = false, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 10, Title = "Issue 10", IsOpen = true, Url = "u10", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByRepositoryQueryHandler(context);
        var query = new IssuesByRepositoryQuery(_mockProcessor.Object) { RepositoryId = repo.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey(1));
        Assert.True(result.ContainsKey(2));
        Assert.True(result.ContainsKey(10));
        Assert.Equal("Issue 1", result[1].Title);
        Assert.Equal("Issue 10", result[10].Title);
    }

    [Fact]
    public async Task HandleAsync_OnlyReturnsIssuesForSpecifiedRepository()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "test/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "test/repo2", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 1, Title = "Repo1 Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo2.Id, Number = 1, Title = "Repo2 Issue", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByRepositoryQueryHandler(context);
        var query = new IssuesByRepositoryQuery(_mockProcessor.Object) { RepositoryId = repo1.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Repo1 Issue", result[1].Title);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenRepositoryNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssuesByRepositoryQueryHandler(context);
        var query = new IssuesByRepositoryQuery(_mockProcessor.Object) { RepositoryId = 99999 };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
