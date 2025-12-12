using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.IssueCommandHandlers;

public class IssueBatchSetParentsCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_ReturnsZero_WhenMapIsEmpty()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>()
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task HandleAsync_SetsParentId_ForSingleIssue()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var parentIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Parent Issue",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        var childIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 2,
            Title = "Child Issue",
            IsOpen = true,
            Url = "url2",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.AddRange(parentIssue, childIssue);
        await context.SaveChangesAsync();

        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>
            {
                { childIssue.Id, parentIssue.Id }
            }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        var updatedChild = await context.Issues.FindAsync(childIssue.Id);
        Assert.NotNull(updatedChild);
        Assert.Equal(parentIssue.Id, updatedChild.ParentIssueId);
    }

    [Fact]
    public async Task HandleAsync_SetsParentIds_ForMultipleIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var parentIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Parent Issue",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        var child1 = new Issue
        {
            RepositoryId = repo.Id,
            Number = 2,
            Title = "Child 1",
            IsOpen = true,
            Url = "url2",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        var child2 = new Issue
        {
            RepositoryId = repo.Id,
            Number = 3,
            Title = "Child 2",
            IsOpen = true,
            Url = "url3",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.AddRange(parentIssue, child1, child2);
        await context.SaveChangesAsync();

        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>
            {
                { child1.Id, parentIssue.Id },
                { child2.Id, parentIssue.Id }
            }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result);
        var updatedChild1 = await context.Issues.FindAsync(child1.Id);
        var updatedChild2 = await context.Issues.FindAsync(child2.Id);
        Assert.Equal(parentIssue.Id, updatedChild1!.ParentIssueId);
        Assert.Equal(parentIssue.Id, updatedChild2!.ParentIssueId);
    }

    [Fact]
    public async Task HandleAsync_RemovesParent_WhenParentIdIsNull()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var parentIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Parent Issue",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(parentIssue);
        await context.SaveChangesAsync();

        var childIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 2,
            Title = "Child Issue",
            IsOpen = true,
            Url = "url2",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f },
            ParentIssueId = parentIssue.Id // Has parent
        };
        context.Issues.Add(childIssue);
        await context.SaveChangesAsync();

        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>
            {
                { childIssue.Id, null } // Remove parent
            }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        var updatedChild = await context.Issues.FindAsync(childIssue.Id);
        Assert.NotNull(updatedChild);
        Assert.Null(updatedChild.ParentIssueId);
    }

    [Fact]
    public async Task HandleAsync_IgnoresNonExistentIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var parentIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Parent Issue",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(parentIssue);
        await context.SaveChangesAsync();

        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>
            {
                { 99999, parentIssue.Id } // Non-existent child
            }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, result); // No updates since child doesn't exist
    }

    [Fact]
    public async Task HandleAsync_DoesNotCountUnchangedIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var parentIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Parent Issue",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(parentIssue);
        await context.SaveChangesAsync();

        var childIssue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 2,
            Title = "Child Issue",
            IsOpen = true,
            Url = "url2",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f },
            ParentIssueId = parentIssue.Id // Already has this parent
        };
        context.Issues.Add(childIssue);
        await context.SaveChangesAsync();

        var handler = new IssueBatchSetParentsCommandHandler(context);
        var command = new IssueBatchSetParentsCommand(_mockExecutor.Object)
        {
            ChildToParentMap = new Dictionary<int, int?>
            {
                { childIssue.Id, parentIssue.Id } // Same parent - no change
            }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, result); // No actual change
    }
}
