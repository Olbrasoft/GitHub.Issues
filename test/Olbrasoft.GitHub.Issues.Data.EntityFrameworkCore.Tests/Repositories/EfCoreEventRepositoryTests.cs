using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.Repositories;

public class EfCoreEventRepositoryTests
{
    [Fact]
    public async Task GetAllEventTypesAsync_ReturnsDictionary_KeyedByName()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var eventType1 = new EventType { Name = "closed" };
        var eventType2 = new EventType { Name = "reopened" };
        context.EventTypes.AddRange(eventType1, eventType2);
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);

        // Act
        var result = await repository.GetAllEventTypesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("closed"));
        Assert.True(result.ContainsKey("reopened"));
        Assert.Equal(eventType1.Id, result["closed"].Id);
        Assert.Equal(eventType2.Id, result["reopened"].Id);
    }

    [Fact]
    public async Task GetEventTypeByIdAsync_ReturnsEventType_WhenExists()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var eventType = new EventType { Name = "labeled" };
        context.EventTypes.Add(eventType);
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);

        // Act
        var result = await repository.GetEventTypeByIdAsync(eventType.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventType.Id, result.Id);
        Assert.Equal("labeled", result.Name);
    }

    [Fact]
    public async Task GetEventTypeByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new EfCoreEventRepository(context);

        // Act
        var result = await repository.GetEventTypeByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddEventTypeAsync_AddsEventType()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repository = new EfCoreEventRepository(context);
        var eventType = new EventType { Name = "mentioned" };

        // Act
        await repository.AddEventTypeAsync(eventType);

        // Assert
        var saved = await context.EventTypes.FindAsync(eventType.Id);
        Assert.NotNull(saved);
        Assert.Equal("mentioned", saved.Name);
    }

    [Fact]
    public async Task GetExistingEventIdsByRepositoryAsync_ReturnsEventIds()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        var issue = new Issue { Title = "Test", Number = 1, Repository = repo, RepositoryId = repo.Id, Url = "url" };
        var eventType = new EventType { Name = "closed" };
        context.Repositories.Add(repo);
        context.Issues.Add(issue);
        context.EventTypes.Add(eventType);
        await context.SaveChangesAsync();

        var event1 = new IssueEvent { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 123, CreatedAt = DateTimeOffset.UtcNow };
        var event2 = new IssueEvent { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 456, CreatedAt = DateTimeOffset.UtcNow };
        context.IssueEvents.AddRange(event1, event2);
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);

        // Act
        var result = await repository.GetExistingEventIdsByRepositoryAsync(repo.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(123L, result);
        Assert.Contains(456L, result);
    }

    [Fact]
    public async Task GetExistingEventIdsByRepositoryAsync_ReturnsEmpty_WhenNoEvents()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var repository = new EfCoreEventRepository(context);

        // Act
        var result = await repository.GetExistingEventIdsByRepositoryAsync(repo.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddEventsBatchAsync_AddsNewEvents()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        var issue = new Issue { Title = "Test", Number = 1, Repository = repo, Url = "url" };
        var eventType = new EventType { Name = "closed" };
        context.Repositories.Add(repo);
        context.Issues.Add(issue);
        context.EventTypes.Add(eventType);
        await context.SaveChangesAsync();

        var events = new List<IssueEvent>
        {
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 100, CreatedAt = DateTimeOffset.UtcNow },
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 200, CreatedAt = DateTimeOffset.UtcNow },
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 300, CreatedAt = DateTimeOffset.UtcNow }
        };
        var existingIds = new HashSet<long>();

        var repository = new EfCoreEventRepository(context);

        // Act
        var count = await repository.AddEventsBatchAsync(events, existingIds);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(3, existingIds.Count);
        Assert.Contains(100L, existingIds);
        Assert.Contains(200L, existingIds);
        Assert.Contains(300L, existingIds);

        var savedEvents = context.IssueEvents.ToList();
        Assert.Equal(3, savedEvents.Count);
    }

    [Fact]
    public async Task AddEventsBatchAsync_SkipsExistingEvents()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        var issue = new Issue { Title = "Test", Number = 1, Repository = repo, Url = "url" };
        var eventType = new EventType { Name = "closed" };
        context.Repositories.Add(repo);
        context.Issues.Add(issue);
        context.EventTypes.Add(eventType);
        await context.SaveChangesAsync();

        var events = new List<IssueEvent>
        {
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 100, CreatedAt = DateTimeOffset.UtcNow },
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 200, CreatedAt = DateTimeOffset.UtcNow },
            new() { IssueId = issue.Id, EventTypeId = eventType.Id, GitHubEventId = 300, CreatedAt = DateTimeOffset.UtcNow }
        };
        var existingIds = new HashSet<long> { 200L }; // Event 200 already exists

        var repository = new EfCoreEventRepository(context);

        // Act
        var count = await repository.AddEventsBatchAsync(events, existingIds);

        // Assert
        Assert.Equal(2, count); // Only 2 new events added (100 and 300)
        Assert.Equal(3, existingIds.Count); // Now contains 100, 200, 300
        Assert.Contains(100L, existingIds);
        Assert.Contains(200L, existingIds);
        Assert.Contains(300L, existingIds);

        var savedEvents = context.IssueEvents.ToList();
        Assert.Equal(2, savedEvents.Count); // Only 2 saved (100 and 300)
    }

    [Fact]
    public async Task AddEventsBatchAsync_HandlesLargeBatches()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        var issue = new Issue { Title = "Test", Number = 1, Repository = repo, Url = "url" };
        var eventType = new EventType { Name = "closed" };
        context.Repositories.Add(repo);
        context.Issues.Add(issue);
        context.EventTypes.Add(eventType);
        await context.SaveChangesAsync();

        // Create 250 events (more than BatchSaveSize of 100)
        var events = Enumerable.Range(1, 250)
            .Select(i => new IssueEvent
            {
                IssueId = issue.Id,
                EventTypeId = eventType.Id,
                GitHubEventId = i,
                CreatedAt = DateTimeOffset.UtcNow
            })
            .ToList();
        var existingIds = new HashSet<long>();

        var repository = new EfCoreEventRepository(context);

        // Act
        var count = await repository.AddEventsBatchAsync(events, existingIds);

        // Assert
        Assert.Equal(250, count);
        Assert.Equal(250, existingIds.Count);

        var savedEvents = context.IssueEvents.ToList();
        Assert.Equal(250, savedEvents.Count);
    }
}
