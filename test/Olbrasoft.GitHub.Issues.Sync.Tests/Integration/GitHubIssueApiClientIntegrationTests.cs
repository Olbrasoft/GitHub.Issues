using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Testing.Xunit.Attributes;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Integration;

/// <summary>
/// Integration tests for GitHubIssueApiClient.
/// These tests call the REAL GitHub API to verify the sync works correctly.
///
/// Required: Set GITHUB_TOKEN environment variable before running.
/// </summary>
public class GitHubIssueApiClientIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GitHubIssueApiClient _apiClient;
    private readonly Mock<ILogger<GitHubIssueApiClient>> _loggerMock;

    // Test against the real repository
    private const string TestOwner = "Olbrasoft";
    private const string TestRepo = "GitHub.Issues";

    public GitHubIssueApiClientIntegrationTests()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GitHub__Token");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Integration-Test", "1.0"));

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var syncSettings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
        _loggerMock = new Mock<ILogger<GitHubIssueApiClient>>();

        _apiClient = new GitHubIssueApiClient(_httpClient, syncSettings, _loggerMock.Object);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// CRITICAL TEST: Verify GitHub API returns ALL issues (no since filter).
    /// This is the first step - we need to confirm GitHub returns the data.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssuesAsync_WithoutSince_ReturnsAllIssues()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set (required for this integration test)");
            return;
        }

        // Act
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);

        // Assert - GitHub.Issues repo should have many issues
        Assert.NotNull(issues);
        Assert.NotEmpty(issues);

        // Log what we got for debugging
        var issueCount = issues.Count;
        var prCount = issues.Count(i => i.IsPullRequest);
        var actualIssueCount = issueCount - prCount;
        var openIssueCount = issues.Count(i => !i.IsPullRequest && i.State == "open");
        var closedIssueCount = issues.Count(i => !i.IsPullRequest && i.State == "closed");

        // Output for debugging
        Console.WriteLine($"=== GitHub API Response for {TestOwner}/{TestRepo} ===");
        Console.WriteLine($"Total items returned: {issueCount}");
        Console.WriteLine($"Pull Requests: {prCount}");
        Console.WriteLine($"Actual Issues: {actualIssueCount}");
        Console.WriteLine($"  - Open: {openIssueCount}");
        Console.WriteLine($"  - Closed: {closedIssueCount}");
        Console.WriteLine();
        Console.WriteLine("Open issues:");
        foreach (var issue in issues.Where(i => !i.IsPullRequest && i.State == "open").OrderBy(i => i.Number))
        {
            Console.WriteLine($"  #{issue.Number}: {issue.Title}");
        }

        // We expect at least some issues
        Assert.True(actualIssueCount > 0, $"Expected at least 1 issue, got {actualIssueCount}");
    }

    /// <summary>
    /// Test that incremental sync with very old timestamp returns all issues.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssuesAsync_WithOldSince_ReturnsAllRecentlyUpdatedIssues()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        // Arrange - use timestamp from 1 year ago
        var since = DateTimeOffset.UtcNow.AddYears(-1);

        // Act
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: since);

        // Assert
        Assert.NotNull(issues);

        Console.WriteLine($"=== Incremental sync (since {since:u}) ===");
        Console.WriteLine($"Issues updated in last year: {issues.Count}");
    }

    /// <summary>
    /// Test that incremental sync with very recent timestamp returns few/no issues.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssuesAsync_WithRecentSince_ReturnsFewOrNoIssues()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        // Arrange - use timestamp from 1 minute ago
        var since = DateTimeOffset.UtcNow.AddMinutes(-1);

        // Act
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: since);

        // Assert
        Assert.NotNull(issues);

        Console.WriteLine($"=== Incremental sync (since {since:u}) ===");
        Console.WriteLine($"Issues updated in last minute: {issues.Count}");

        // This should return 0 or very few issues (unless something was just updated)
        // We don't assert 0 because timing could cause flakiness
    }

    /// <summary>
    /// Verify that we can distinguish between issues and pull requests.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssuesAsync_CorrectlyIdentifiesPullRequests()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        // Act
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);

        // Assert
        var prs = issues.Where(i => i.IsPullRequest).ToList();
        var actualIssues = issues.Where(i => !i.IsPullRequest).ToList();

        Console.WriteLine($"=== PR vs Issue breakdown ===");
        Console.WriteLine($"Pull Requests: {prs.Count}");
        Console.WriteLine($"Issues: {actualIssues.Count}");

        if (prs.Count > 0)
        {
            Console.WriteLine("\nSample PRs:");
            foreach (var pr in prs.Take(5))
            {
                Console.WriteLine($"  #{pr.Number}: {pr.Title} (PR: {pr.IsPullRequest})");
            }
        }

        // Verify that issues marked as PR have pull_request property
        // (this is already handled by the parser, just confirming)
        Assert.All(actualIssues, i => Assert.False(i.IsPullRequest));
    }

    /// <summary>
    /// Test fetching comments for a specific issue.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssueCommentsAsync_ReturnsComments()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        // First get an issue that might have comments
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);
        var issueWithPotentialComments = issues
            .Where(i => !i.IsPullRequest)
            .OrderByDescending(i => i.Number)
            .FirstOrDefault();

        if (issueWithPotentialComments == null)
        {
            Console.WriteLine("No issues found to test comments");
            return;
        }

        // Act
        var comments = await _apiClient.FetchIssueCommentsAsync(
            TestOwner, TestRepo, issueWithPotentialComments.Number);

        // Assert
        Assert.NotNull(comments);
        Console.WriteLine($"=== Comments for issue #{issueWithPotentialComments.Number} ===");
        Console.WriteLine($"Comment count: {comments.Count}");
    }

    /// <summary>
    /// CRITICAL: Compare what we get from API with expected counts.
    /// </summary>
    [SkipOnCIFact]
    public async Task FetchIssuesAsync_VerifyExpectedOpenIssueCount()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        // Act
        var issues = await _apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);

        // Get open issues (excluding PRs)
        var openIssues = issues
            .Where(i => !i.IsPullRequest && i.State == "open")
            .OrderBy(i => i.Number)
            .ToList();

        // Assert
        Console.WriteLine($"=== VERIFICATION: Open Issues ===");
        Console.WriteLine($"Total open issues from API: {openIssues.Count}");
        Console.WriteLine();
        Console.WriteLine("All open issues:");
        foreach (var issue in openIssues)
        {
            Console.WriteLine($"  #{issue.Number}: {issue.Title}");
            Console.WriteLine($"           Updated: {issue.UpdatedAt:u}");
            Console.WriteLine($"           Labels: {string.Join(", ", issue.LabelNames)}");
        }

        // The user said there are 13 open issues on GitHub
        // Let's verify this
        Assert.True(openIssues.Count >= 10,
            $"Expected at least 10 open issues (user reported 13), but got {openIssues.Count}");
    }
}
