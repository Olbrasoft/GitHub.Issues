using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

public class IssueByRepositoryAndNumberQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsIssue_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "Test Issue",
            IsOpen = true,
            Url = "https://github.com/test/repo/issues/42",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var handler = new IssueByRepositoryAndNumberQueryHandler(context);
        var query = new IssueByRepositoryAndNumberQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Number = 42
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.Number);
        Assert.Equal("Test Issue", result.Title);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new IssueByRepositoryAndNumberQueryHandler(context);
        var query = new IssueByRepositoryAndNumberQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Number = 99
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

        var issue = new Issue
        {
            RepositoryId = repo1.Id,
            Number = 42,
            Title = "Test Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var handler = new IssueByRepositoryAndNumberQueryHandler(context);
        var query = new IssueByRepositoryAndNumberQuery(_mockProcessor.Object)
        {
            RepositoryId = repo2.Id,
            Number = 42
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_IncludesLabels()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var label = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(label);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Bug Report",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        context.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = label.Id });
        await context.SaveChangesAsync();

        var handler = new IssueByRepositoryAndNumberQueryHandler(context);
        var query = new IssueByRepositoryAndNumberQuery(_mockProcessor.Object)
        {
            RepositoryId = repo.Id,
            Number = 1
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.IssueLabels);
        Assert.Equal("bug", result.IssueLabels.First().Label.Name);
    }
}
