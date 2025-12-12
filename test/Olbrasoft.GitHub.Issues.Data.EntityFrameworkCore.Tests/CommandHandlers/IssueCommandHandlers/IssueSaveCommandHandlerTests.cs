using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.IssueCommandHandlers;

public class IssueSaveCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_CreatesNewIssue_WhenNotExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new IssueSaveCommandHandler(context);
        var command = new IssueSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "New Issue",
            IsOpen = true,
            Url = "https://github.com/test/repo/issues/42",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f, 2.0f, 3.0f }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(42, result.Number);
        Assert.Equal("New Issue", result.Title);
        Assert.True(result.IsOpen);

        // Verify persisted
        var saved = await context.Issues.FindAsync(result.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task HandleAsync_UpdatesExistingIssue_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var existingIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "Old Title",
            IsOpen = true,
            Url = "old-url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(existingIssue);
        await context.SaveChangesAsync();

        var handler = new IssueSaveCommandHandler(context);
        var command = new IssueSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Number = 42,
            Title = "Updated Title",
            IsOpen = false,
            Url = "new-url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 2.0f, 3.0f }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingIssue.Id, result.Id);
        Assert.Equal("Updated Title", result.Title);
        Assert.False(result.IsOpen);
        Assert.Equal("new-url", result.Url);

        // Verify only one issue exists
        Assert.Single(context.Issues);
    }

    [Fact]
    public async Task HandleAsync_PreservesExistingEmbedding_WhenNewIsNull()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var existingEmbedding = new float[] { 1.0f, 2.0f, 3.0f };
        var existingIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = existingEmbedding
        };
        context.Issues.Add(existingIssue);
        await context.SaveChangesAsync();

        var handler = new IssueSaveCommandHandler(context);
        var command = new IssueSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Updated",
            IsOpen = false,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = null!
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Embedding should be preserved
        Assert.Equal(existingEmbedding, result.Embedding);
    }

    [Fact]
    public async Task HandleAsync_DistinguishesByRepositoryId()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo1 = new Repository { FullName = "test/repo1", GitHubId = 1, HtmlUrl = "url1" };
        var repo2 = new Repository { FullName = "test/repo2", GitHubId = 2, HtmlUrl = "url2" };
        context.Repositories.AddRange(repo1, repo2);
        await context.SaveChangesAsync();

        var existingIssue = new Issue
        {
            RepositoryId = repo1.Id,
            Number = 1,
            Title = "Repo1 Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(existingIssue);
        await context.SaveChangesAsync();

        var handler = new IssueSaveCommandHandler(context);
        var command = new IssueSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo2.Id,
            Number = 1,
            Title = "Repo2 Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Should create new issue for different repo
        Assert.NotEqual(existingIssue.Id, result.Id);
        Assert.Equal(2, context.Issues.Count());
    }
}
