using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.RepositoryCommandHandlers;

public class RepositoryUpdateLastSyncedCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_UpdatesTimestamp_WhenRepositoryExists()
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

        var handler = new RepositoryUpdateLastSyncedCommandHandler(context);
        var syncTime = DateTimeOffset.UtcNow;
        var command = new RepositoryUpdateLastSyncedCommand(_mockExecutor.Object)
        {
            RepositoryId = repository.Id,
            LastSyncedAt = syncTime
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify timestamp was updated
        var updated = await context.Repositories.FindAsync(repository.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastSyncedAt);
        Assert.Equal(syncTime.UtcDateTime, updated.LastSyncedAt.Value.UtcDateTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenRepositoryNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositoryUpdateLastSyncedCommandHandler(context);
        var command = new RepositoryUpdateLastSyncedCommand(_mockExecutor.Object)
        {
            RepositoryId = 99999,
            LastSyncedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_OverwritesPreviousTimestamp()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var oldTime = DateTimeOffset.UtcNow.AddDays(-7);
        var repository = new Repository
        {
            FullName = "test/repo",
            GitHubId = 12345,
            HtmlUrl = "https://example.com",
            LastSyncedAt = oldTime
        };
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();

        var handler = new RepositoryUpdateLastSyncedCommandHandler(context);
        var newTime = DateTimeOffset.UtcNow;
        var command = new RepositoryUpdateLastSyncedCommand(_mockExecutor.Object)
        {
            RepositoryId = repository.Id,
            LastSyncedAt = newTime
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updated = await context.Repositories.FindAsync(repository.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastSyncedAt);
        Assert.Equal(newTime.UtcDateTime, updated.LastSyncedAt.Value.UtcDateTime, TimeSpan.FromMilliseconds(1));
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
            HtmlUrl = "https://example.com"
        };
        context1.Repositories.Add(repository);
        await context1.SaveChangesAsync();

        var handler = new RepositoryUpdateLastSyncedCommandHandler(context1);
        var syncTime = DateTimeOffset.UtcNow;
        var command = new RepositoryUpdateLastSyncedCommand(_mockExecutor.Object)
        {
            RepositoryId = repository.Id,
            LastSyncedAt = syncTime
        };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Use new context to verify persistence
        await using var context2 = TestDbContextFactory.Create(dbName);
        var loaded = await context2.Repositories.FindAsync(repository.Id);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.LastSyncedAt);
    }
}
