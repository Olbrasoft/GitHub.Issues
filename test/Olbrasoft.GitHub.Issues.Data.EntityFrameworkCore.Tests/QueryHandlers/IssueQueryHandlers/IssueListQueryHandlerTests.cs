using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

public class IssueListQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmptyPage_WhenRepositoryIdsIsEmpty()
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

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = Array.Empty<int>() };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsIssuesForRepository()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "Issue 1", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 2, Title = "Issue 2", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = new[] { repo.Id } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public async Task HandleAsync_OrdersIssuesByNumberDescending()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo.Id, Number = 1, Title = "First", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 5, Title = "Fifth", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo.Id, Number = 3, Title = "Third", IsOpen = true, Url = "u3", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = new[] { repo.Id } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Results[0].IssueNumber);
        Assert.Equal(3, result.Results[1].IssueNumber);
        Assert.Equal(1, result.Results[2].IssueNumber);
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

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object)
        {
            RepositoryIds = new[] { repo.Id },
            State = "open"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.True(result.Results[0].IsOpen);
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

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object)
        {
            RepositoryIds = new[] { repo.Id },
            State = "closed"
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.False(result.Results[0].IsOpen);
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
                Title = $"Issue {i:D2}",
                IsOpen = true,
                Url = $"url{i}",
                GitHubUpdatedAt = DateTimeOffset.UtcNow,
                SyncedAt = DateTimeOffset.UtcNow,
                Embedding = new float[] { 1.0f }
            });
        }
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object)
        {
            RepositoryIds = new[] { repo.Id },
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
    public async Task HandleAsync_ReturnsCorrectTotalPages()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        for (int i = 1; i <= 15; i++)
        {
            context.Issues.Add(new Issue
            {
                RepositoryId = repo.Id,
                Number = i,
                Title = $"Issue {i}",
                IsOpen = true,
                Url = $"url{i}",
                GitHubUpdatedAt = DateTimeOffset.UtcNow,
                SyncedAt = DateTimeOffset.UtcNow,
                Embedding = new float[] { 1.0f }
            });
        }
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object)
        {
            RepositoryIds = new[] { repo.Id },
            PageSize = 7
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(3, result.TotalPages); // ceil(15/7) = 3
    }

    [Fact]
    public async Task HandleAsync_FiltersByMultipleRepositoryIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "owner/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "owner/repo2", GitHubId = 2, HtmlUrl = "url2" };
        var repo3 = new Repository { FullName = "owner/repo3", GitHubId = 3, HtmlUrl = "url3" };
        context.Repositories.AddRange(repo1, repo2, repo3);
        await context.SaveChangesAsync();

        context.Issues.AddRange(
            new Issue { RepositoryId = repo1.Id, Number = 1, Title = "Repo1 Issue", IsOpen = true, Url = "u1", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo2.Id, Number = 1, Title = "Repo2 Issue", IsOpen = true, Url = "u2", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } },
            new Issue { RepositoryId = repo3.Id, Number = 1, Title = "Repo3 Issue", IsOpen = true, Url = "u3", GitHubUpdatedAt = DateTimeOffset.UtcNow, SyncedAt = DateTimeOffset.UtcNow, Embedding = new float[] { 1.0f } }
        );
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object)
        {
            RepositoryIds = new[] { repo1.Id, repo3.Id }
        };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Results, r => r.RepositoryFullName == "owner/repo1");
        Assert.Contains(result.Results, r => r.RepositoryFullName == "owner/repo3");
        Assert.DoesNotContain(result.Results, r => r.RepositoryFullName == "owner/repo2");
    }

    [Fact]
    public async Task HandleAsync_IncludesLabelsInResults()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var label1 = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        var label2 = new Label { RepositoryId = repo.Id, Name = "enhancement", Color = "00ff00" };
        context.Labels.AddRange(label1, label2);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Issue with Labels",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        context.IssueLabels.AddRange(
            new IssueLabel { IssueId = issue.Id, LabelId = label1.Id },
            new IssueLabel { IssueId = issue.Id, LabelId = label2.Id }
        );
        await context.SaveChangesAsync();

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = new[] { repo.Id } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(2, result.Results[0].Labels.Count);
        Assert.Contains(result.Results[0].Labels, l => l.Name == "bug");
        Assert.Contains(result.Results[0].Labels, l => l.Name == "enhancement");
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenNoMatchingRepository()
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

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = new[] { 99999 } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSimilarityOfOne()
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

        var handler = new IssueListQueryHandler(context);
        var query = new IssueListQuery(_mockProcessor.Object) { RepositoryIds = new[] { repo.Id } };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(1.0f, result.Results[0].Similarity);
    }
}
