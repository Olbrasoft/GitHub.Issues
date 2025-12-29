using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Database;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Tests for DatabaseStatusService.
/// Note: In-memory provider doesn't support migrations, so we can only test
/// limited scenarios using a real (non-in-memory) database context.
/// These tests focus on the service's error handling and constructor behavior.
/// </summary>
public class DatabaseStatusServiceTests
{
    private readonly Mock<ILogger<DatabaseStatusService>> _mockLogger = new();
    private readonly Mock<IIssueRepository> _mockIssueRepository = new();
    private readonly Mock<IRepositoryRepository> _mockRepositoryRepository = new();

    [Fact]
    public void Constructor_AcceptsValidDependencies()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act
        var service = new DatabaseStatusService(
            context,
            _mockIssueRepository.Object,
            _mockRepositoryRepository.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetStatusAsync_WhenExceptionThrown_ReturnsConnectionError()
    {
        // Arrange - create a mock context that throws when used
        var mockContext = new Mock<GitHubDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<GitHubDbContext>());

        var service = new DatabaseStatusService(
            mockContext.Object,
            _mockIssueRepository.Object,
            _mockRepositoryRepository.Object,
            _mockLogger.Object);

        // Act - The mock context will throw because it's not properly configured
        var result = await service.GetStatusAsync(CancellationToken.None);

        // Assert - Should return error status
        Assert.False(result.CanConnect);
        Assert.Equal(DatabaseStatusCode.ConnectionError, result.StatusCode);
        Assert.NotNull(result.StatusMessage);
    }

    [Fact]
    public void DatabaseStatusCode_HasExpectedValues()
    {
        // Assert - Verify all expected status codes exist
        Assert.Equal(5, Enum.GetValues<DatabaseStatusCode>().Length);
        Assert.True(Enum.IsDefined(DatabaseStatusCode.Healthy));
        Assert.True(Enum.IsDefined(DatabaseStatusCode.EmptyDatabase));
        Assert.True(Enum.IsDefined(DatabaseStatusCode.PendingMigrations));
        Assert.True(Enum.IsDefined(DatabaseStatusCode.NoData));
        Assert.True(Enum.IsDefined(DatabaseStatusCode.ConnectionError));
    }

    [Fact]
    public void DatabaseStatus_Record_HasExpectedProperties()
    {
        // Arrange & Act
        var status = new DatabaseStatus
        {
            CanConnect = true,
            HasTables = true,
            PendingMigrationCount = 1,
            PendingMigrations = ["test-migration"],
            IssueCount = 10,
            RepositoryCount = 5,
            StatusCode = DatabaseStatusCode.Healthy,
            StatusMessage = "Test",
            ErrorMessage = null
        };

        // Assert
        Assert.True(status.CanConnect);
        Assert.True(status.HasTables);
        Assert.Equal(1, status.PendingMigrationCount);
        Assert.Single(status.PendingMigrations);
        Assert.Equal(10, status.IssueCount);
        Assert.Equal(5, status.RepositoryCount);
        Assert.Equal(DatabaseStatusCode.Healthy, status.StatusCode);
        Assert.Equal("Test", status.StatusMessage);
        Assert.Null(status.ErrorMessage);
    }

    [Fact]
    public void MigrationResult_Record_HasExpectedProperties()
    {
        // Arrange & Act
        var result = new MigrationResult
        {
            Success = true,
            MigrationsApplied = 2,
            AppliedMigrations = ["migration1", "migration2"],
            ErrorMessage = null
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.MigrationsApplied);
        Assert.Equal(2, result.AppliedMigrations.Count);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void MigrationResult_Record_CanRepresentFailure()
    {
        // Arrange & Act
        var result = new MigrationResult
        {
            Success = false,
            MigrationsApplied = 0,
            AppliedMigrations = Array.Empty<string>(),
            ErrorMessage = "Test error"
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.MigrationsApplied);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal("Test error", result.ErrorMessage);
    }
}
