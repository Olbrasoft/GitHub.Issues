using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.CommandHandlers.LabelCommandHandlers;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.CommandHandlers.LabelCommandHandlers;

public class LabelSaveCommandHandlerTests
{
    private readonly Mock<ICommandExecutor> _mockExecutor = new();

    [Fact]
    public async Task HandleAsync_CreatesNewLabel_WhenNotExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new LabelSaveCommandHandler(context);
        var command = new LabelSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Name = "bug",
            Color = "ff0000"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("bug", result.Name);
        Assert.Equal("ff0000", result.Color);

        // Verify persisted
        var saved = await context.Labels.FindAsync(result.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task HandleAsync_UpdatesExistingLabel_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var existingLabel = new Label
        {
            RepositoryId = repo.Id,
            Name = "bug",
            Color = "00ff00"
        };
        context.Labels.Add(existingLabel);
        await context.SaveChangesAsync();

        var handler = new LabelSaveCommandHandler(context);
        var command = new LabelSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Name = "bug",
            Color = "ff0000"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingLabel.Id, result.Id);
        Assert.Equal("ff0000", result.Color);
        Assert.Single(context.Labels);
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

        var existingLabel = new Label
        {
            RepositoryId = repo1.Id,
            Name = "bug",
            Color = "00ff00"
        };
        context.Labels.Add(existingLabel);
        await context.SaveChangesAsync();

        var handler = new LabelSaveCommandHandler(context);
        var command = new LabelSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo2.Id,
            Name = "bug",
            Color = "ff0000"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Should create new label for different repo
        Assert.NotEqual(existingLabel.Id, result.Id);
        Assert.Equal(2, context.Labels.Count());
    }

    [Fact]
    public async Task HandleAsync_UsesDefaultColor_WhenNotSpecified()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var handler = new LabelSaveCommandHandler(context);
        var command = new LabelSaveCommand(_mockExecutor.Object)
        {
            RepositoryId = repo.Id,
            Name = "test"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal("ededed", result.Color);
    }
}
