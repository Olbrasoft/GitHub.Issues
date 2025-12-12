using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.RepositoryCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.RepositoryCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.RepositoryCommandHandlers;

public class RepositorySaveCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_CreatesNewRepository_WhenNotExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositorySaveCommandHandler(context);
        var command = new RepositorySaveCommand(_mockExecutor.Object)
        {
            GitHubId = 12345,
            FullName = "microsoft/dotnet",
            HtmlUrl = "https://github.com/microsoft/dotnet"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("microsoft/dotnet", result.FullName);
        Assert.Equal(12345, result.GitHubId);
        Assert.Equal("https://github.com/microsoft/dotnet", result.HtmlUrl);
        Assert.True(result.Id > 0);

        // Verify it was persisted
        var savedRepo = await context.Repositories.FindAsync(result.Id);
        Assert.NotNull(savedRepo);
        Assert.Equal("microsoft/dotnet", savedRepo.FullName);
    }

    [Fact]
    public async Task HandleAsync_UpdatesExistingRepository_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var existingRepo = new Repository
        {
            GitHubId = 11111,
            FullName = "microsoft/dotnet",
            HtmlUrl = "https://old-url.com"
        };
        context.Repositories.Add(existingRepo);
        await context.SaveChangesAsync();

        var handler = new RepositorySaveCommandHandler(context);
        var command = new RepositorySaveCommand(_mockExecutor.Object)
        {
            GitHubId = 22222,
            FullName = "microsoft/dotnet",
            HtmlUrl = "https://new-url.com"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingRepo.Id, result.Id);
        Assert.Equal(22222, result.GitHubId);
        Assert.Equal("https://new-url.com", result.HtmlUrl);
        Assert.Single(context.Repositories);
    }

    [Fact]
    public async Task HandleAsync_LooksUpByFullName_NotByGitHubId()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var existingRepo = new Repository
        {
            GitHubId = 12345,
            FullName = "owner/repo1",
            HtmlUrl = "https://github.com/owner/repo1"
        };
        context.Repositories.Add(existingRepo);
        await context.SaveChangesAsync();

        var handler = new RepositorySaveCommandHandler(context);
        var command = new RepositorySaveCommand(_mockExecutor.Object)
        {
            GitHubId = 12345,
            FullName = "owner/repo2",
            HtmlUrl = "https://github.com/owner/repo2"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Should create new because FullName is different
        Assert.NotEqual(existingRepo.Id, result.Id);
        Assert.Equal(2, context.Repositories.Count());
    }

    [Fact]
    public async Task HandleAsync_ReturnsCompleteEntity()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var handler = new RepositorySaveCommandHandler(context);
        var command = new RepositorySaveCommand(_mockExecutor.Object)
        {
            GitHubId = 99999,
            FullName = "test/repo",
            HtmlUrl = "https://example.com"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(99999, result.GitHubId);
        Assert.Equal("test/repo", result.FullName);
        Assert.Equal("https://example.com", result.HtmlUrl);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task HandleAsync_PersistsToDatabase()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context1 = TestDbContextFactory.Create(dbName);
        var handler = new RepositorySaveCommandHandler(context1);
        var command = new RepositorySaveCommand(_mockExecutor.Object)
        {
            GitHubId = 12345,
            FullName = "test/persist",
            HtmlUrl = "https://example.com"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Verify with new context
        await using var context2 = TestDbContextFactory.Create(dbName);
        var loaded = await context2.Repositories.FindAsync(result.Id);
        Assert.NotNull(loaded);
        Assert.Equal("test/persist", loaded.FullName);
    }
}
