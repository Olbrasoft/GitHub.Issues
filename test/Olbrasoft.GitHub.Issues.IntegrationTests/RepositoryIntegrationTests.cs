using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.Testing.Xunit.Attributes;
using Xunit;
using Xunit.Abstractions;

namespace Olbrasoft.GitHub.Issues.IntegrationTests;

/// <summary>
/// Integration tests for repository pattern with real database.
/// These tests verify repositories work correctly with SQL Server and are skipped on CI.
/// </summary>
public class RepositoryIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly GitHubDbContext _context;
    private readonly EfCoreCachedTextRepository _cachedTextRepository;

    public RepositoryIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Load connection string
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<RepositoryIntegrationTests>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "A database connection string must be configured for RepositoryIntegrationTests. " +
                "Configure 'ConnectionStrings:DefaultConnection' in user secrets or set the " +
                "'ConnectionStrings__DefaultConnection' environment variable.");

        var options = new DbContextOptionsBuilder<GitHubDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new GitHubDbContext(options);
        _cachedTextRepository = new EfCoreCachedTextRepository(_context);
    }

    [SkipOnCIFact]
    public async Task GetByIssueAsync_WithExistingCache_ReturnsData()
    {
        // Arrange - Find any existing cached text
        var anyCached = await _context.CachedTexts
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (anyCached == null)
        {
            _output.WriteLine("No cached texts in database - skipping test");
            return;
        }

        // Act
        var result = await _cachedTextRepository.GetByIssueAsync(
            anyCached.IssueId,
            anyCached.LanguageId,
            anyCached.TextTypeId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(anyCached.IssueId, result.IssueId);
        Assert.Equal(anyCached.LanguageId, result.LanguageId);
        Assert.Equal(anyCached.TextTypeId, result.TextTypeId);

        _output.WriteLine($"Found cached text for Issue {result.IssueId}");
        _output.WriteLine($"Language: {result.LanguageId}, TextType: {result.TextTypeId}");
        _output.WriteLine($"Content length: {result.Content?.Length ?? 0} chars");
    }

    [SkipOnCIFact]
    public async Task GetMultipleCachedTextsAsync_WithMultipleIssues_ReturnsBatch()
    {
        // Arrange - Get first 5 issue IDs that have cached texts
        var issueIds = await _context.CachedTexts
            .AsNoTracking()
            .Select(c => c.IssueId)
            .Distinct()
            .Take(5)
            .ToListAsync();

        if (!issueIds.Any())
        {
            _output.WriteLine("No cached texts in database - skipping test");
            return;
        }

        var languageId = (int)LanguageCode.EnUS;
        var textTypeIds = new[] { (int)TextTypeCode.Title, (int)TextTypeCode.ListSummary };

        _output.WriteLine($"Querying for {issueIds.Count} issues");

        // Act
        var results = await _cachedTextRepository.GetMultipleCachedTextsAsync(
            issueIds,
            languageId,
            textTypeIds);

        // Assert
        Assert.NotNull(results);
        _output.WriteLine($"Found {results.Count} cached texts");

        foreach (var result in results)
        {
            _output.WriteLine($"  Issue {result.IssueId}: Lang={result.LanguageId}, Type={result.TextTypeId}");
        }
    }

    [SkipOnCIFact]
    public async Task GetIssuesByIdsAsync_WithExistingIssues_ReturnsIssues()
    {
        // Arrange - Get first 3 issue IDs
        var issueIds = await _context.Issues
            .AsNoTracking()
            .Select(i => i.Id)
            .Take(3)
            .ToListAsync();

        if (!issueIds.Any())
        {
            _output.WriteLine("No issues in database - skipping test");
            return;
        }

        _output.WriteLine($"Querying for {issueIds.Count} issues");

        // Act
        var results = await _cachedTextRepository.GetIssuesByIdsAsync(issueIds);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(issueIds.Count, results.Count);

        foreach (var (id, issue) in results)
        {
            _output.WriteLine($"  Issue {id}: {issue.Title}");
            _output.WriteLine($"    Number: {issue.Number}, Repository: {issue.RepositoryId}");
        }
    }

    [SkipOnCIFact]
    public async Task GetStatisticsAsync_ReturnsValidStatistics()
    {
        // Act
        var stats = await _cachedTextRepository.GetStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalRecords >= 0);

        _output.WriteLine($"Total cached texts: {stats.TotalRecords}");
        _output.WriteLine($"Languages: {stats.ByLanguage.Count}");
        _output.WriteLine($"Text types: {stats.ByTextType.Count}");

        foreach (var (lang, count) in stats.ByLanguage)
        {
            _output.WriteLine($"  {lang}: {count} texts");
        }

        foreach (var (type, count) in stats.ByTextType)
        {
            _output.WriteLine($"  {type}: {count} texts");
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
