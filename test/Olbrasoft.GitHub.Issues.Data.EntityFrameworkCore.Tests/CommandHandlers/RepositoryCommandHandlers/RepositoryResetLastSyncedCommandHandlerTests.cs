using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.RepositoryCommandHandlers;

public class RepositoryResetLastSyncedCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_ResetsTimestampToNull_WhenRepositoryExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryResetLastSyncedCommandHandler(context);
        var command = new RepositoryResetLastSyncedCommand(_mockExecutor.Object) { FullName = "test/repo" };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify timestamp is now null
        var updated = await context.Repositories.FindAsync(repository.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.LastSyncedAt);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenRepositoryNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoryResetLastSyncedCommandHandler(context);
        var command = new RepositoryResetLastSyncedCommand(_mockExecutor.Object) { FullName = "nonexistent/repo" };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_WorksWhenTimestampAlreadyNull()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = null
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryResetLastSyncedCommandHandler(context);
        var command = new RepositoryResetLastSyncedCommand(_mockExecutor.Object) { FullName = "test/repo" };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updated = await context.Repositories.FindAsync(repository.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.LastSyncedAt);
    }

    [Fact]
    public async Task HandleAsync_UsesExactFullNameMatch()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new Repository
        {
            FullName = "Test/Repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryResetLastSyncedCommandHandler(context);
        var command = new RepositoryResetLastSyncedCommand(_mockExecutor.Object) { FullName = "test/repo" };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - In-memory provider is case-sensitive
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_PersistsChanges()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context1 = TestDbContextFactory.Create(dbName);
        var repository = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        context1.Repositories.Add(repository);
        await context1.SaveChangesAsync();

        var handler = new RepositoryResetLastSyncedCommandHandler(context1);
        var command = new RepositoryResetLastSyncedCommand(_mockExecutor.Object) { FullName = "test/repo" };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Use new context to verify persistence
        await using var context2 = TestDbContextFactory.Create(dbName);
        var loaded = await context2.Repositories.FindAsync(repository.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded.LastSyncedAt);
    }
}
