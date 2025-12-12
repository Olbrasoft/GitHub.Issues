using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

public class IssueTextSearchQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmptyPage_WhenSearchTextIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmptyPage_WhenSearchTextIsWhitespace()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "   " };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task HandleAsync_FindsMatchingIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Bug in authentication", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Feature request", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 3, Title = "Bug fix needed", IsOpen = false, Url = "u3", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "bug" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Results, r => Assert.Contains("bug", r.Title.ToLower()));
    }

    [Fact]
    public async Task HandleAsync_IsCaseInsensitive()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.Add(new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "BUG in AUTHENTICATION",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        });
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "bug" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
    }

    [Fact]
    public async Task HandleAsync_FiltersOpenState()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Test Issue Open", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Test Issue Closed", IsOpen = false, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "test", State = "open" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.True(result.Results.First().IsOpen);
    }

    [Fact]
    public async Task HandleAsync_FiltersClosedState()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Test Issue Open", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Test Issue Closed", IsOpen = false, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "test", State = "closed" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.False(result.Results.First().IsOpen);
    }

    [Fact]
    public async Task HandleAsync_FiltersByRepositoryIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "test/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "test/repo2", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 1, Title = "Test Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo2.Id, Number = 1, Title = "Test Issue", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object)
        {
            SearchText = "test",
            RepositoryIds = new[] { repo1.Id }
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal("test/repo1", result.Results.First().RepositoryFullName);
    }

    [Fact]
    public async Task HandleAsync_SupportsPagination()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        for (int i = 1; i <= 25; i++)
        {
            context.Issues.Add(new Issue
            {
                RepositoryId = repo.Id,
                Number = i,
                Title = $"Test Issue {i:D2}",
                IsOpen = true,
                Url = $"url{i}",
                GitHubUpdatedAt = DateTimeOffset.UtcNow,
                SyncedAt = DateTimeOffset.UtcNow,
                Embedding = new float[] { 1.0f }
            });
        }
        await context.SaveChangesAsync();

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object)
        {
            SearchText = "test",
            Page = 2,
            PageSize = 10
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Results.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task HandleAsync_IncludesLabelsInResults()
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
            Title = "Test Issue with Labels",
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

        var handler = new IssueTextSearchQueryHandler(context);
        var query = new IssueTextSearchQuery(_mockProcessor.Object) { SearchText = "test" };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Single(result.Results.First().Labels);
        Assert.Equal("bug", result.Results.First().Labels.First().Name);
    }
}
