using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.RepositoryQueryHandlers;

public class RepositoriesSearchQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenTermIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.Add(new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" });
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenTermIsWhitespace()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.Add(new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" });
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "   " };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_FindsMatchingRepositories()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.AddRange(
            new Repository { FullName = "microsoft/dotnet", GitHubId = 1, HtmlUrl = "url1" },
            new Repository { FullName = "microsoft/vscode", GitHubId = 2, HtmlUrl = "url2" },
            new Repository { FullName = "google/angular", GitHubId = 3, HtmlUrl = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "microsoft" };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains("microsoft", r.FullName.ToLower()));
    }

    [Fact]
    public async Task HandleAsync_IsCaseInsensitive()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.Add(new Repository { FullName = "Microsoft/DotNet", GitHubId = 1, HtmlUrl = "url" });
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "MICROSOFT" };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Microsoft/DotNet", result[0].FullName);
    }

    [Fact]
    public async Task HandleAsync_RespectsMaxResults()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        for (int i = 1; i <= 20; i++)
        {
            context.Repositories.Add(new Repository { FullName = $"test/repo{i}", GitHubId = i, HtmlUrl = $"url{i}" });
        }
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "test", MaxResults = 5 };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task HandleAsync_ReturnsResultsOrderedAlphabetically()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.AddRange(
            new Repository { FullName = "zebra/repo", GitHubId = 1, HtmlUrl = "url1" },
            new Repository { FullName = "alpha/repo", GitHubId = 2, HtmlUrl = "url2" },
            new Repository { FullName = "beta/repo", GitHubId = 3, HtmlUrl = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "repo" };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal("alpha/repo", result[0].FullName);
        Assert.Equal("beta/repo", result[1].FullName);
        Assert.Equal("zebra/repo", result[2].FullName);
    }

    [Fact]
    public async Task HandleAsync_DefaultMaxResultsIs15()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        for (int i = 1; i <= 20; i++)
        {
            context.Repositories.Add(new Repository { FullName = $"test/repo{i:D2}", GitHubId = i, HtmlUrl = $"url{i}" });
        }
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "test" };

        // Act
        var result = (await handler.HandleAsync(query, CancellationToken.None)).ToList();

        // Assert
        Assert.Equal(15, result.Count);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenNoMatches()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.Add(new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" });
        await context.SaveChangesAsync();

        var handler = new RepositoriesSearchQueryHandler(context);
        var query = new RepositoriesSearchQuery(_mockProcessor.Object) { Term = "nonexistent" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
