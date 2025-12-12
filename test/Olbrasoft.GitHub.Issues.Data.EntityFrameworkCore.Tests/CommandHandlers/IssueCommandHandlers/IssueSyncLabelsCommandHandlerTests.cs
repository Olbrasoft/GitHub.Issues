using Microsoft.EntityFrameworkCore;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.IssueCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.IssueCommandHandlers;

public class IssueSyncLabelsCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenIssueNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = 999,
            RepositoryId = 1,
            LabelNames = new List<string> { "bug" }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_AddsNewLabels()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var bugLabel = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        var enhancementLabel = new Label { RepositoryId = repo.Id, Name = "enhancement", Color = "00ff00" };
        context.Labels.AddRange(bugLabel, enhancementLabel);
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

        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            RepositoryId = repo.Id,
            LabelNames = new List<string> { "bug", "enhancement" }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues
            .Include(i => i.IssueLabels)
            .ThenInclude(il => il.Label)
            .FirstAsync(i => i.Id == issue.Id);
        Assert.Equal(2, updatedIssue.IssueLabels.Count);
        Assert.Contains(updatedIssue.IssueLabels, il => il.Label.Name == "bug");
        Assert.Contains(updatedIssue.IssueLabels, il => il.Label.Name == "enhancement");
    }

    [Fact]
    public async Task HandleAsync_RemovesOldLabels()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var bugLabel = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        var enhancementLabel = new Label { RepositoryId = repo.Id, Name = "enhancement", Color = "00ff00" };
        context.Labels.AddRange(bugLabel, enhancementLabel);
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

        // Add both labels initially
        context.IssueLabels.AddRange(
            new IssueLabel { IssueId = issue.Id, LabelId = bugLabel.Id },
            new IssueLabel { IssueId = issue.Id, LabelId = enhancementLabel.Id }
        );
        await context.SaveChangesAsync();

        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            RepositoryId = repo.Id,
            LabelNames = new List<string> { "bug" } // Only keep bug
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues
            .Include(i => i.IssueLabels)
            .ThenInclude(il => il.Label)
            .FirstAsync(i => i.Id == issue.Id);
        Assert.Single(updatedIssue.IssueLabels);
        Assert.Equal("bug", updatedIssue.IssueLabels.First().Label.Name);
    }

    [Fact]
    public async Task HandleAsync_ClearsAllLabels_WhenEmptyList()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var bugLabel = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(bugLabel);
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

        context.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = bugLabel.Id });
        await context.SaveChangesAsync();

        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            RepositoryId = repo.Id,
            LabelNames = new List<string>() // Empty list
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues
            .Include(i => i.IssueLabels)
            .FirstAsync(i => i.Id == issue.Id);
        Assert.Empty(updatedIssue.IssueLabels);
    }

    [Fact]
    public async Task HandleAsync_IgnoresNonExistentLabels()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var bugLabel = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(bugLabel);
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

        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            RepositoryId = repo.Id,
            LabelNames = new List<string> { "bug", "nonexistent-label" }
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues
            .Include(i => i.IssueLabels)
            .ThenInclude(il => il.Label)
            .FirstAsync(i => i.Id == issue.Id);
        Assert.Single(updatedIssue.IssueLabels);
        Assert.Equal("bug", updatedIssue.IssueLabels.First().Label.Name);
    }

    [Fact]
    public async Task HandleAsync_DoesNotDuplicateExistingLabels()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var bugLabel = new Label { RepositoryId = repo.Id, Name = "bug", Color = "ff0000" };
        context.Labels.Add(bugLabel);
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

        // Add label initially
        context.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = bugLabel.Id });
        await context.SaveChangesAsync();

        var handler = new IssueSyncLabelsCommandHandler(context);
        var command = new IssueSyncLabelsCommand(_mockExecutor.Object)
        {
            IssueId = issue.Id,
            RepositoryId = repo.Id,
            LabelNames = new List<string> { "bug" } // Same label as already exists
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedIssue = await context.Issues
            .Include(i => i.IssueLabels)
            .FirstAsync(i => i.Id == issue.Id);
        Assert.Single(updatedIssue.IssueLabels);
    }
}
