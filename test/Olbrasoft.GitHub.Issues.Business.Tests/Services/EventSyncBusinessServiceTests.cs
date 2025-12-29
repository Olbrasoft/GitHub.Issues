using Moq;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Data.Commands.EventCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.EventQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class EventSyncBusinessServiceTests
{
    private readonly Mock<IMediator> _mockMediator = new();

    private EventSyncBusinessService CreateService()
    {
        return new EventSyncBusinessService(_mockMediator.Object);
    }

    [Fact]
    public async Task GetAllEventTypesAsync_ReturnsDictionary()
    {
        // Arrange
        var eventTypes = new Dictionary<string, EventType>
        {
            ["opened"] = new() { Id = 1, Name = "opened" },
            ["closed"] = new() { Id = 2, Name = "closed" }
        };
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<EventTypesAllQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventTypes);

        var service = CreateService();

        // Act
        var result = await service.GetAllEventTypesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("opened"));
        Assert.True(result.ContainsKey("closed"));
    }

    [Fact]
    public async Task GetAllEventTypesAsync_ReturnsEmptyDictionary_WhenNoEventTypes()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<EventTypesAllQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, EventType>());

        var service = CreateService();

        // Act
        var result = await service.GetAllEventTypesAsync(CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetExistingEventIdsAsync_ReturnsHashSet()
    {
        // Arrange
        var eventIds = new HashSet<long> { 100, 200, 300 };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueEventIdsByRepositoryQuery>(q => q.RepositoryId == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventIds);

        var service = CreateService();

        // Act
        var result = await service.GetExistingEventIdsAsync(1, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(100L, result);
        Assert.Contains(200L, result);
        Assert.Contains(300L, result);
    }

    [Fact]
    public async Task GetExistingEventIdsAsync_ReturnsEmptyHashSet_WhenNoEvents()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueEventIdsByRepositoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<long>());

        var service = CreateService();

        // Act
        var result = await service.GetExistingEventIdsAsync(1, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveEventsBatchAsync_ReturnsNumberOfSavedEvents()
    {
        // Arrange
        var events = new List<IssueEventData>
        {
            new(IssueId: 1, EventTypeId: 1, GitHubEventId: 100, CreatedAt: DateTimeOffset.UtcNow),
            new(IssueId: 1, EventTypeId: 2, GitHubEventId: 200, CreatedAt: DateTimeOffset.UtcNow)
        };
        var existingIds = new HashSet<long>();

        _mockMediator.Setup(m => m.MediateAsync(It.Is<IssueEventsSaveBatchCommand>(c =>
            c.Events.Count == 2 && c.ExistingEventIds == existingIds),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = CreateService();

        // Act
        var result = await service.SaveEventsBatchAsync(events, existingIds, CancellationToken.None);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task SaveEventsBatchAsync_ReturnsZero_WhenAllEventsExist()
    {
        // Arrange
        var events = new List<IssueEventData>
        {
            new(IssueId: 1, EventTypeId: 1, GitHubEventId: 100, CreatedAt: DateTimeOffset.UtcNow)
        };
        var existingIds = new HashSet<long> { 100 };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueEventsSaveBatchCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();

        // Act
        var result = await service.SaveEventsBatchAsync(events, existingIds, CancellationToken.None);

        // Assert
        Assert.Equal(0, result);
    }
}
