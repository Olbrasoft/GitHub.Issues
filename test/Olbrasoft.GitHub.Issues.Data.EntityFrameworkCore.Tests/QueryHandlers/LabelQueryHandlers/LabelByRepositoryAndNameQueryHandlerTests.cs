using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.LabelQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.LabelQueryHandlers;

public class LabelByRepositoryAndNameQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsLabel_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var label = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(label);
        await context.SaveChangesAsync();

        var handler = new LabelByRepositoryAndNameQueryHandler(context);
        var query = new LabelByRepositoryAndNameQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Name = "bug"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bug", result.Name);
        Assert.Equal("ff0000", result.Color);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new LabelByRepositoryAndNameQueryHandler(context);
        var query = new LabelByRepositoryAndNameQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Name = "nonexistent"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenWrongRepository()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "test/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "test/repo2", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        var label = new Label { RepositoryId = repo1.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(label);
        await context.SaveChangesAsync();

        var handler = new LabelByRepositoryAndNameQueryHandler(context);
        var query = new LabelByRepositoryAndNameQuery(_mockProcessor.Object)
        {
            RepositoryId = repo2.Id,
            Name = "bug"
        };

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
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var label = new Label { RepositoryId = repo.Id, Name = "Bug", Color = "ff0000" };
        context.Labels.Add(label);
        await context.SaveChangesAsync();

        var handler = new LabelByRepositoryAndNameQueryHandler(context);
        var query = new LabelByRepositoryAndNameQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Name = "bug"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert - In-memory provider is case-sensitive
        Assert.Null(result);
    }
}
