using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.RepositoryQueryHandlers;

public class RepositoriesByIdsQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenIdsListIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.Add(new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" });
        await context.SaveChangesAsync();

        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = Array.Empty<int>() };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSingleRepository_WhenSingleIdProvided()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = new[] { repo.Id } };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(repo.Id, result[0].Id);
        Assert.Equal("test/repo", result[0].FullName);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMultipleRepositories()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "owner/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "owner/repo2", GitHubId = 2, HtmlUrl = "url2" };
        var repo3 = new Repository { FullName = "owner/repo3", GitHubId = 3, HtmlUrl = "url3" };
        context.Repositories.AddRange(repo1, repo2, repo3);
        await context.SaveChangesAsync();

        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = new[] { repo1.Id, repo3.Id } };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == repo1.Id);
        Assert.Contains(result, r => r.Id == repo3.Id);
        Assert.DoesNotContain(result, r => r.Id == repo2.Id);
    }

    [Fact]
    public async Task HandleAsync_IgnoresNonExistentIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = new[] { repo.Id, 99999 } };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(repo.Id, result[0].Id);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenDatabaseIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = new[] { 1, 2, 3 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsOnlyIdAndFullName()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new RepositoriesByIdsQueryHandler(context);
        var query = new RepositoriesByIdsQuery(_mockProcessor.Object) { Ids = new[] { repo.Id } };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).Single();

        // Assert - DTO should only have Id and FullName
        Assert.Equal(repo.Id, result.Id);
        Assert.Equal("test/repo", result.FullName);
    }
}
