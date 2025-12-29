using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

/// <summary>
/// Tests for MigrationManager.
/// Note: In-memory provider doesn't support migrations, so we can only test
/// limited scenarios by using test and mocked <see cref="GitHubDbContext"/> instances.
/// These tests focus on the service's constructor behavior and error handling when
/// the database context is misconfigured or unavailable.
/// </summary>
public class MigrationManagerTests
{
    private readonly Mock<ILogger<MigrationManager>> _mockLogger = new();

    [Fact]
    public void Constructor_AcceptsValidDependencies()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act
        var service = new MigrationManager(context, _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_ThrowsWhenContextIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationManager(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        // Arrange
        using var context = Data.EntityFrameworkCore.Tests.TestDbContextFactory.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationManager(context, null!));
    }

    [Fact]
    public async Task ApplyMigrationsAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // Arrange - create a mock context that throws when used
        var mockContext = new Mock<GitHubDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<GitHubDbContext>());

        var service = new MigrationManager(mockContext.Object, _mockLogger.Object);

        // Act - The mock context will throw because it's not properly configured
        var result = await service.ApplyMigrationsAsync(CancellationToken.None);

        // Assert - Should return failure result
        Assert.False(result.Success);
        Assert.Equal(0, result.MigrationsApplied);
        Assert.NotNull(result.ErrorMessage);
    }

}
