using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

public class IssuesByNumbersQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenIssueNumbersIsEmpty()
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
            Title = "Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        });
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = Array.Empty<int>() };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_FindsIssueByNumber()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 42, Title = "Target Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 100, Title = "Other Issue", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = new[] { 42 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0].IssueNumber);
        Assert.Equal("Target Issue", result[0].Title);
    }

    [Fact]
    public async Task HandleAsync_FindsMultipleIssuesByNumbers()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Issue 2", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 3, Title = "Issue 3", IsOpen = true, Url = "u3", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = new[] { 1, 3 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.IssueNumber == 1);
        Assert.Contains(result, r => r.IssueNumber == 3);
        Assert.DoesNotContain(result, r => r.IssueNumber == 2);
    }

    [Fact]
    public async Task HandleAsync_FiltersByRepositoryIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "owner/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "owner/repo2", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 42, Title = "Issue in Repo1", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo2.Id, Number = 42, Title = "Issue in Repo2", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object)
        {
            IssueNumbers = new[] { 42 },
            RepositoryIds = new[] { repo1.Id }
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("owner/repo1", result[0].RepositoryFullName);
    }

    [Fact]
    public async Task HandleAsync_FiltersByRepositoryName()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "microsoft/dotnet", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "google/angular", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 100, Title = "Microsoft Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo2.Id, Number = 100, Title = "Google Issue", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object)
        {
            IssueNumbers = new[] { 100 },
            RepositoryName = "microsoft"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("microsoft/dotnet", result[0].RepositoryFullName);
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
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Open Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Closed Issue", IsOpen = false, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object)
        {
            IssueNumbers = new[] { 1, 2 },
            State = "open"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsOpen);
        Assert.Equal(1, result[0].IssueNumber);
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
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Open Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Closed Issue", IsOpen = false, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object)
        {
            IssueNumbers = new[] { 1, 2 },
            State = "closed"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.False(result[0].IsOpen);
        Assert.Equal(2, result[0].IssueNumber);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSimilarityOfOne_ForExactMatch()
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
            Title = "Test Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        });
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = new[] { 1 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(1.0, result[0].Similarity);
    }

    [Fact]
    public async Task HandleAsync_IgnoresNonExistentNumbers()
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
            Title = "Existing Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        });
        await context.SaveChangesAsync();

        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = new[] { 1, 999, 1000 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].IssueNumber);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenDatabaseIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssuesByNumbersQueryHandler(context);
        var query = new IssuesByNumbersQuery(_mockProcessor.Object) { IssueNumbers = new[] { 1, 2, 3 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
