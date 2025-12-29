using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.Testing.Xunit.Attributes;
using Xunit;
using Xunit.Abstractions;

namespace Olbrasoft.GitHub.Issues.IntegrationTests;

/// <summary>
/// Integration tests for database operations.
/// These tests use real SQL Server database and are skipped on CI environments.
/// </summary>
public class DatabaseIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly GitHubDbContext _context;
    private readonly string _connectionString;

    public DatabaseIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Load connection string from configuration
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseIntegrationTests>()
            .AddEnvironmentVariables()
            .Build();

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=GitHubIssues;User Id=sa;Password=Tuma/*-+;TrustServerCertificate=True;Encrypt=True;";

        var options = new DbContextOptionsBuilder<GitHubDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        _context = new GitHubDbContext(options);
    }

    [SkipOnCIFact]
    public async Task CanConnect_WithValidConnectionString_ReturnsTrue()
    {
        // Act
        var canConnect = await _context.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect, "Should be able to connect to database");

        _output.WriteLine($"Connection successful");
        _output.WriteLine($"Connection string: {MaskConnectionString(_connectionString)}");
    }

    [SkipOnCIFact]
    public async Task GetPendingMigrations_ReturnsExpectedResult()
    {
        // Act
        var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();

        // Assert - No strict assertion, just logging for information
        _output.WriteLine($"Applied migrations: {appliedMigrations.Count()}");
        _output.WriteLine($"Pending migrations: {pendingMigrations.Count()}");

        foreach (var migration in appliedMigrations)
        {
            _output.WriteLine($"  Applied: {migration}");
        }

        foreach (var migration in pendingMigrations)
        {
            _output.WriteLine($"  Pending: {migration}");
        }
    }

    [SkipOnCIFact]
    public async Task Database_HasRequiredTables()
    {
        // Act
        var canConnect = await _context.Database.CanConnectAsync();
        Assert.True(canConnect, "Must be connected to check tables");

        // Check if main tables exist by querying them
        var issuesExist = await _context.Issues.AnyAsync();
        var repositoriesExist = await _context.Repositories.AnyAsync();

        // Assert - Log results (tables may be empty but should exist)
        _output.WriteLine($"Issues table accessible: {true}"); // If we got here, table exists
        _output.WriteLine($"Repositories table accessible: {true}");
        _output.WriteLine($"Issues count: {await _context.Issues.CountAsync()}");
        _output.WriteLine($"Repositories count: {await _context.Repositories.CountAsync()}");
    }

    [SkipOnCIFact]
    public async Task CachedTexts_CanQueryAndCount()
    {
        // Arrange
        var canConnect = await _context.Database.CanConnectAsync();
        Assert.True(canConnect);

        // Act
        var cachedTextCount = await _context.CachedTexts.CountAsync();
        var languages = await _context.Languages.ToListAsync();
        var textTypes = await _context.TextTypes.ToListAsync();

        // Assert - Log information
        _output.WriteLine($"CachedTexts count: {cachedTextCount}");
        _output.WriteLine($"Languages count: {languages.Count}");
        _output.WriteLine($"TextTypes count: {textTypes.Count}");

        foreach (var lang in languages)
        {
            _output.WriteLine($"  Language: {lang.CultureName} (ID: {lang.Id})");
        }

        foreach (var type in textTypes)
        {
            _output.WriteLine($"  TextType: {type.Name} (ID: {type.Id})");
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask password in connection string for logging
        var passwordPattern = @"Password=[^;]*";
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            passwordPattern,
            "Password=***");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
