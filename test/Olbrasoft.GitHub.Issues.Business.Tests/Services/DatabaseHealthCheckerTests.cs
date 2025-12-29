using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Tests for DatabaseHealthChecker.
/// Note: In-memory provider doesn't support migrations, so we can only test
/// limited scenarios using a real (non-in-memory) database context.
/// These tests focus on the service's error handling and constructor behavior.
/// </summary>
public class DatabaseHealthCheckerTests
{
    private readonly Mock<ILogger<DatabaseHealthChecker>> _mockLogger = new();
    private readonly Mock<IIssueRepository> _mockIssueRepository = new();
    private readonly Mock<IRepositoryRepository> _mockRepositoryRepository = new();

    [Fact]
    public void Constructor_AcceptsValidDependencies()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act
        var service = new DatabaseHealthChecker(
            context,
            _mockIssueRepository.Object,
            _mockRepositoryRepository.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_ThrowsWhenContextIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseHealthChecker(
                null!,
                _mockIssueRepository.Object,
                _mockRepositoryRepository.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenIssueRepositoryIsNull()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseHealthChecker(
                context,
                null!,
                _mockRepositoryRepository.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenRepositoryRepositoryIsNull()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseHealthChecker(
                context,
                _mockIssueRepository.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseHealthChecker(
                context,
                _mockIssueRepository.Object,
                _mockRepositoryRepository.Object,
                null!));
    }

    [Fact]
    public async Task GetStatusAsync_WhenExceptionThrown_ReturnsConnectionError()
    {
        // Arrange - create a mock context that throws when used
        var mockContext = new Mock<GitHubDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<GitHubDbContext>());

        var service = new DatabaseHealthChecker(
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

}
