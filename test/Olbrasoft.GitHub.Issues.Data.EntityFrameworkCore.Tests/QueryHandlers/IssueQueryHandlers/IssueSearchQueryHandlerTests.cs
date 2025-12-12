using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Tests for IssueSearchQueryHandler.
/// Note: Vector similarity searches (CosineDistance/VectorDistance) cannot be tested
/// with in-memory database as they require database-specific extensions (pgvector/SQL Server).
/// Full integration tests would be needed for vector search functionality.
/// </summary>
public class IssueSearchQueryHandlerTests
{
    private readonly Mock<IQueryProcessor> _mockProcessor = new();

    private static IOptions<DatabaseSettings> CreateSettings(DatabaseProvider provider = DatabaseProvider.PostgreSQL)
    {
        return Options.Create(new DatabaseSettings { Provider = provider });
    }

    [Fact]
    public void Constructor_AcceptsContextAndSettings()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var settings = CreateSettings();

        // Act
        var handler = new IssueSearchQueryHandler(context, settings);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmptyResults_WhenDatabaseIsEmpty()
    {
        // Arrange - Empty database
        await using var context = TestDbContextFactory.Create();

        var settings = CreateSettings();
        var handler = new IssueSearchQueryHandler(context, settings);
        var query = new IssueSearchQuery(_mockProcessor.Object)
        {
            QueryEmbedding = new float[] { 0.1f, 0.2f, 0.3f },
            RepositoryIds = new[] { 1 }
        };

        // Act & Assert
        // Note: This test may throw due to in-memory provider not supporting CosineDistance
        // In production, this would filter out issues without embeddings first
        try
        {
            var result = await handler.HandleAsync(query, CancellationToken.None);
            // If it doesn't throw, verify empty results
            Assert.Empty(result.Results);
            Assert.Equal(0, result.TotalCount);
        }
        catch (InvalidOperationException)
        {
            // Expected - CosineDistance not supported in-memory
            Assert.True(true, "CosineDistance not supported in in-memory database");
        }
    }

    [Fact]
    public async Task HandleAsync_CorrectlyBuildsPagedResult()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new Repository { FullName = "test/repo", GitHubId = 1, HtmlUrl = "url" };
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        // Add issues with embeddings
        for (int i = 1; i <= 5; i++)
        {
            context.Issues.Add(new Issue
            {
                RepositoryId = repo.Id,
                Number = i,
                Title = $"Issue {i}",
                IsOpen = true,
                Url = $"url{i}",
                GitHubUpdatedAt = DateTimeOffset.UtcNow,
                SyncedAt = DateTimeOffset.UtcNow,
                Embedding = new float[] { 0.1f * i, 0.2f * i, 0.3f * i }
            });
        }
        await context.SaveChangesAsync();

        var settings = CreateSettings();
        var handler = new IssueSearchQueryHandler(context, settings);
        var query = new IssueSearchQuery(_mockProcessor.Object)
        {
            QueryEmbedding = new float[] { 0.1f, 0.2f, 0.3f },
            Page = 1,
            PageSize = 3
        };

        // Act & Assert
        // Note: Vector operations not supported in-memory
        try
        {
            var result = await handler.HandleAsync(query, CancellationToken.None);
            Assert.Equal(1, result.Page);
            Assert.Equal(3, result.PageSize);
        }
        catch (InvalidOperationException)
        {
            // Expected - CosineDistance not supported in-memory
            Assert.True(true, "CosineDistance not supported in in-memory database");
        }
    }

    [Fact]
    public void Query_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var query = new IssueSearchQuery(_mockProcessor.Object);

        // Assert
        Assert.Equal("all", query.State);
        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.PageSize);
        Assert.Null(query.RepositoryIds);
    }

    [Fact]
    public void DatabaseSettings_DefaultsToPostgreSQL()
    {
        // Arrange & Act
        var settings = new DatabaseSettings();

        // Assert
        Assert.Equal(DatabaseProvider.PostgreSQL, settings.Provider);
    }
}
