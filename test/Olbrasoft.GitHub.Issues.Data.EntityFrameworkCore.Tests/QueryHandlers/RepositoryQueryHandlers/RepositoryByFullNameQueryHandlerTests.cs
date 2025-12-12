using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.RepositoryQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.RepositoryQueryHandlers;

public class RepositoryByFullNameQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsRepository_WhenFullNameMatches()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new Repository
        {
            FullName = "microsoft/dotnet",
            GitHubId = 12345,
            HtmlUrl = "https://github.com/microsoft/dotnet"
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryByFullNameQueryHandler(context);
        var query = new RepositoryByFullNameQuery(_mockProcessor.Object) { FullName = "microsoft/dotnet" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("microsoft/dotnet", result.FullName);
        Assert.Equal(12345, result.GitHubId);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenFullNameNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoryByFullNameQueryHandler(context);
        var query = new RepositoryByFullNameQuery(_mockProcessor.Object) { FullName = "nonexistent/repo" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_IsCaseSensitive()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new Repository
        {
            FullName = "Microsoft/DotNet",
            GitHubId = 12345,
            HtmlUrl = "https://github.com/Microsoft/DotNet"
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryByFullNameQueryHandler(context);
        var query = new RepositoryByFullNameQuery(_mockProcessor.Object) { FullName = "microsoft/dotnet" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert - In-memory provider is case-sensitive by default
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFirstMatch_WhenMultipleRepositoriesExist()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.Repositories.AddRange(
            new Repository { FullName = "owner/repo1", GitHubId = 1, HtmlUrl = "url1" },
            new Repository { FullName = "owner/repo2", GitHubId = 2, HtmlUrl = "url2" },
            new Repository { FullName = "owner/repo3", GitHubId = 3, HtmlUrl = "url3" }
        );
        await context.SaveChangesAsync();

        var handler = new RepositoryByFullNameQueryHandler(context);
        var query = new RepositoryByFullNameQuery(_mockProcessor.Object) { FullName = "owner/repo2" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("owner/repo2", result.FullName);
        Assert.Equal(2, result.GitHubId);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenDatabaseIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoryByFullNameQueryHandler(context);
        var query = new RepositoryByFullNameQuery(_mockProcessor.Object) { FullName = "any/repo" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
