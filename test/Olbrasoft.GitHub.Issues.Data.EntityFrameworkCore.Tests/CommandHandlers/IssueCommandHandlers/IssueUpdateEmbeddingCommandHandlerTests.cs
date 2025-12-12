using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.IssueCommandHandlers;

public class IssueUpdateEmbeddingCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenIssueNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssueUpdateEmbeddingCommandHandler(context);
        var command = new IssueUpdateEmbeddingCommand(_mockExecutor.Object)
        {
            IssueId = 999,
            Embedding = new float[] { 1.0f, 2.0f }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_UpdatesEmbedding_WhenIssueExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Test Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var newEmbedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var handler = new IssueUpdateEmbeddingCommandHandler(context);
        var command = new IssueUpdateEmbeddingCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            Embedding = newEmbedding
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues.FindAsync(issue.Id);
        Assert.NotNull(updatedIssue);
        Assert.Equal(newEmbedding, updatedIssue.Embedding);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTrue_EvenWithEmptyEmbedding()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Test Issue",
            IsOpen = true,
            Url = "url",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f, 2.0f }
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var handler = new IssueUpdateEmbeddingCommandHandler(context);
        var command = new IssueUpdateEmbeddingCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            Embedding = Array.Empty<float>()
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues.FindAsync(issue.Id);
        Assert.NotNull(updatedIssue);
        Assert.Empty(updatedIssue.Embedding);
    }

    [Fact]
    public async Task HandleAsync_DoesNotAffectOtherIssues()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var issue1 = new Issue
        {
            RepositoryId = repo.Id,
            Number = 1,
            Title = "Issue 1",
            IsOpen = true,
            Url = "url1",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 1.0f }
        };
        var issue2 = new Issue
        {
            RepositoryId = repo.Id,
            Number = 2,
            Title = "Issue 2",
            IsOpen = true,
            Url = "url2",
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Embedding = new float[] { 2.0f }
        };
        context.Issues.AddRange(issue1, issue2);
        await context.SaveChangesAsync();

        var handler = new IssueUpdateEmbeddingCommandHandler(context);
        var command = new IssueUpdateEmbeddingCommand(_mockExecutor.Object)
        {
            IssueId = issue1.Id,
            Embedding = new float[] { 9.0f }
        };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Issue 2 should be unchanged
        var unchangedIssue = await context.Issues.FindAsync(issue2.Id);
        Assert.NotNull(unchangedIssue);
        Assert.Equal(new float[] { 2.0f }, unchangedIssue.Embedding);
    }
}
