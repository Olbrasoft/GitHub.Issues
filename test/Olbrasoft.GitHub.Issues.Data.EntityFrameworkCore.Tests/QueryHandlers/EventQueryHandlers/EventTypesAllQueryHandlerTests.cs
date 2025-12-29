using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.EventQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.EventQueryHandlers;

public class EventTypesAllQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    [Fact]
    public async Task HandleAsync_ReturnsEmptyDictionary_WhenNoEventTypes()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new EfCoreEventRepository(context);
        var handler = new EventTypesAllQueryHandler(repository);
        var query = new EventTypesAllQuery(_mockProcessor.Object);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsDictionaryKeyedByName()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.EventTypes.AddRange(
            new EventType { Name = "opened" },
            new EventType { Name = "closed" },
            new EventType { Name = "labeled" }
        );
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);
        var handler = new EventTypesAllQueryHandler(repository);
        var query = new EventTypesAllQuery(_mockProcessor.Object);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("opened"));
        Assert.True(result.ContainsKey("closed"));
        Assert.True(result.ContainsKey("labeled"));
    }

    [Fact]
    public async Task HandleAsync_ReturnsAllEventTypesWithIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        context.EventTypes.AddRange(
            new EventType { Name = "opened" },
            new EventType { Name = "closed" }
        );
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);
        var handler = new EventTypesAllQueryHandler(repository);
        var query = new EventTypesAllQuery(_mockProcessor.Object);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.All(result.Values, et => Assert.True(et.Id > 0));
    }
}
