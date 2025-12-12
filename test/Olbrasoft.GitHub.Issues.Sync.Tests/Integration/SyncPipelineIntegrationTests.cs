using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;
using Olbrasoft.GitHub.Issues.Text.Transformation.Cohere;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Integration;

/// <summary>
/// Integration tests for the complete sync pipeline.
/// Tests are split into phases to identify exactly where failures occur:
/// 1. GitHub API - fetch issues
/// 2. Cohere API - generate embeddings
/// 3. Full sync pipeline
/// </summary>
public class SyncPipelineIntegrationTests : IDisposable
{
    private const string TestOwner = "Olbrasoft";
    private const string TestRepo = "GitHub.Issues";

    private readonly HttpClient _githubClient;
    private readonly HttpClient _cohereClient;

    public SyncPipelineIntegrationTests()
    {
        // GitHub client
        _githubClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        _githubClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _githubClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _githubClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Integration-Test", "1.0"));

        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(githubToken))
        {
            _githubClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        }

        // Cohere client
        _cohereClient = new HttpClient();
    }

    public void Dispose()
    {
        _githubClient.Dispose();
        _cohereClient.Dispose();
    }

    #region Phase 1: GitHub API Tests

    /// <summary>
    /// PHASE 1A: Test GitHub API directly (raw HTTP).
    /// Verifies that GitHub returns issues correctly.
    /// </summary>
    [Fact]
    public async Task Phase1A_GitHubApi_RawHttp_ReturnsIssues()
    {
        Console.WriteLine("=== PHASE 1A: GitHub API - Raw HTTP ===\n");

        // Act
        var response = await _githubClient.GetAsync($"repos/{TestOwner}/{TestRepo}/issues?state=all&per_page=100");

        // Assert
        Console.WriteLine($"HTTP Status: {response.StatusCode}");
        Assert.True(response.IsSuccessStatusCode, $"GitHub API failed: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        Console.WriteLine($"Total items returned: {items.Count}");

        var issues = items.Where(i => !i.TryGetProperty("pull_request", out _)).ToList();
        var prs = items.Where(i => i.TryGetProperty("pull_request", out _)).ToList();
        var openIssues = issues.Where(i => i.GetProperty("state").GetString() == "open").ToList();

        Console.WriteLine($"Issues: {issues.Count}");
        Console.WriteLine($"Pull Requests: {prs.Count}");
        Console.WriteLine($"Open Issues: {openIssues.Count}");

        Console.WriteLine("\nOpen issues:");
        foreach (var issue in openIssues.OrderBy(i => i.GetProperty("number").GetInt32()))
        {
            var number = issue.GetProperty("number").GetInt32();
            var title = issue.GetProperty("title").GetString();
            Console.WriteLine($"  #{number}: {title}");
        }

        Assert.True(issues.Count > 0, "No issues found");
        Assert.True(openIssues.Count >= 10, $"Expected at least 10 open issues, got {openIssues.Count}");

        Console.WriteLine("\n=== PHASE 1A PASSED ===");
    }

    /// <summary>
    /// PHASE 1B: Test GitHubIssueApiClient service.
    /// Verifies our wrapper parses GitHub response correctly.
    /// </summary>
    [Fact]
    public async Task Phase1B_GitHubIssueApiClient_ReturnsAllIssues()
    {
        Console.WriteLine("=== PHASE 1B: GitHubIssueApiClient Service ===\n");

        // Arrange
        var syncSettings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
        var loggerMock = new Mock<ILogger<GitHubIssueApiClient>>();
        var apiClient = new GitHubIssueApiClient(_githubClient, syncSettings, loggerMock.Object);

        // Act
        var issues = await apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);

        // Assert
        Console.WriteLine($"Total items from service: {issues.Count}");

        var actualIssues = issues.Where(i => !i.IsPullRequest).ToList();
        var prs = issues.Where(i => i.IsPullRequest).ToList();
        var openIssues = actualIssues.Where(i => i.State == "open").ToList();

        Console.WriteLine($"Issues (excluding PRs): {actualIssues.Count}");
        Console.WriteLine($"Pull Requests: {prs.Count}");
        Console.WriteLine($"Open Issues: {openIssues.Count}");

        Console.WriteLine("\nOpen issues:");
        foreach (var issue in openIssues.OrderBy(i => i.Number))
        {
            Console.WriteLine($"  #{issue.Number}: {issue.Title}");
        }

        Assert.True(actualIssues.Count > 0, "No issues returned by service");
        Assert.True(openIssues.Count >= 10, $"Expected at least 10 open issues, got {openIssues.Count}");

        Console.WriteLine("\n=== PHASE 1B PASSED ===");
    }

    #endregion

    #region Phase 2: Cohere Embedding Tests

    /// <summary>
    /// PHASE 2A: Test Cohere API directly (raw HTTP).
    /// Verifies Cohere API works and returns embeddings.
    /// </summary>
    [Fact]
    public async Task Phase2A_CohereApi_RawHttp_ReturnsEmbedding()
    {
        Console.WriteLine("=== PHASE 2A: Cohere API - Raw HTTP ===\n");

        var apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY_1");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("SKIP: COHERE_API_KEY_1 not set");
            return;
        }

        // Test texts
        var texts = new[]
        {
            "Bug: Application crashes on startup",
            "Feature request: Add dark mode support",
            "Refactor: Extract service layer"
        };

        foreach (var text in texts)
        {
            Console.WriteLine($"\nTesting: '{text}'");

            var request = new
            {
                texts = new[] { text },
                model = "embed-multilingual-v3.0",
                input_type = "search_document",
                embedding_types = new[] { "float" }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/embed");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _cohereClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"  Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("embeddings", out var embeddings) &&
                    embeddings.TryGetProperty("float", out var floatArrays))
                {
                    var firstEmbedding = floatArrays.EnumerateArray().FirstOrDefault();
                    var dimensions = firstEmbedding.ValueKind == JsonValueKind.Array
                        ? firstEmbedding.GetArrayLength()
                        : 0;
                    Console.WriteLine($"  SUCCESS: {dimensions} dimensions");
                }
            }
            else
            {
                Console.WriteLine($"  ERROR: {responseBody[..Math.Min(200, responseBody.Length)]}");
            }

            await Task.Delay(500); // Avoid rate limiting
        }

        Console.WriteLine("\n=== PHASE 2A COMPLETE ===");
    }

    /// <summary>
    /// PHASE 2B: Test CohereEmbeddingService.
    /// Verifies our service wrapper works correctly.
    /// </summary>
    [Fact]
    public async Task Phase2B_CohereEmbeddingService_GeneratesEmbeddings()
    {
        Console.WriteLine("=== PHASE 2B: CohereEmbeddingService ===\n");

        var apiKey1 = Environment.GetEnvironmentVariable("COHERE_API_KEY_1");
        var apiKey2 = Environment.GetEnvironmentVariable("COHERE_API_KEY_2");
        var apiKey3 = Environment.GetEnvironmentVariable("COHERE_API_KEY_3");

        var apiKeys = new[] { apiKey1, apiKey2, apiKey3 }
            .Where(k => !string.IsNullOrEmpty(k))
            .ToArray();

        if (apiKeys.Length == 0)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        Console.WriteLine($"Configured API keys: {apiKeys.Length}");

        // Create service
        var settings = new EmbeddingSettings
        {
            Provider = EmbeddingProvider.Cohere,
            Cohere = new CohereEmbeddingSettings
            {
                Model = "embed-multilingual-v3.0",
                ApiKeys = apiKeys!
            }
        };

        var loggerMock = new Mock<ILogger<CohereEmbeddingService>>();
        using var httpClient = new HttpClient();
        var embeddingService = new CohereEmbeddingService(
            httpClient,
            Options.Create(settings),
            loggerMock.Object);

        Console.WriteLine($"Service IsConfigured: {embeddingService.IsConfigured}");

        // Test texts
        var texts = new[]
        {
            "Bug: Application crashes on startup",
            "Feature request: Add dark mode support",
            "Refactor: Extract service layer"
        };

        var successCount = 0;
        var failCount = 0;

        foreach (var text in texts)
        {
            Console.WriteLine($"\nTesting: '{text}'");

            var embedding = await embeddingService.GenerateEmbeddingAsync(text, EmbeddingInputType.Document);

            if (embedding != null)
            {
                successCount++;
                Console.WriteLine($"  SUCCESS: {embedding.Length} dimensions");
            }
            else
            {
                failCount++;
                Console.WriteLine($"  FAILED: null returned");
            }

            await Task.Delay(1000); // Longer delay between requests
        }

        Console.WriteLine($"\n=== Results: {successCount} success, {failCount} failed ===");
        Assert.True(successCount > 0, "No embeddings were generated successfully");

        Console.WriteLine("\n=== PHASE 2B COMPLETE ===");
    }

    #endregion

    #region Phase 3: Combined Pipeline Test

    /// <summary>
    /// PHASE 3: Test full pipeline - GitHub + Cohere.
    /// Fetches issues from GitHub and generates embeddings for each.
    /// </summary>
    [Fact]
    public async Task Phase3_FullPipeline_GitHubPlusCohere()
    {
        Console.WriteLine("=== PHASE 3: Full Pipeline (GitHub + Cohere) ===\n");

        var apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY_1");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("SKIP: COHERE_API_KEY_1 not set");
            return;
        }

        // Step 1: Fetch issues from GitHub
        Console.WriteLine("Step 1: Fetching issues from GitHub...");

        var syncSettings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
        var githubLoggerMock = new Mock<ILogger<GitHubIssueApiClient>>();
        var apiClient = new GitHubIssueApiClient(_githubClient, syncSettings, githubLoggerMock.Object);

        var issues = await apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);
        var actualIssues = issues.Where(i => !i.IsPullRequest).Take(5).ToList(); // Test first 5

        Console.WriteLine($"  Fetched {issues.Count} items, testing {actualIssues.Count} issues\n");

        // Step 2: Create embedding service
        var apiKeys = new[] { apiKey, Environment.GetEnvironmentVariable("COHERE_API_KEY_2"), Environment.GetEnvironmentVariable("COHERE_API_KEY_3") }
            .Where(k => !string.IsNullOrEmpty(k))
            .ToArray();

        var embeddingSettings = new EmbeddingSettings
        {
            Provider = EmbeddingProvider.Cohere,
            Cohere = new CohereEmbeddingSettings
            {
                Model = "embed-multilingual-v3.0",
                ApiKeys = apiKeys!
            }
        };

        var cohereLoggerMock = new Mock<ILogger<CohereEmbeddingService>>();
        using var cohereHttpClient = new HttpClient();
        var embeddingService = new CohereEmbeddingService(
            cohereHttpClient,
            Options.Create(embeddingSettings),
            cohereLoggerMock.Object);

        // Step 3: Generate embeddings for each issue
        Console.WriteLine("Step 2: Generating embeddings for issues...\n");

        var successCount = 0;
        var failCount = 0;

        foreach (var issue in actualIssues)
        {
            var textToEmbed = $"{issue.Title}\n{issue.Body ?? ""}";
            Console.WriteLine($"Issue #{issue.Number}: {issue.Title[..Math.Min(40, issue.Title.Length)]}...");

            var embedding = await embeddingService.GenerateEmbeddingAsync(textToEmbed, EmbeddingInputType.Document);

            if (embedding != null)
            {
                successCount++;
                Console.WriteLine($"  SUCCESS: {embedding.Length} dims");
            }
            else
            {
                failCount++;
                Console.WriteLine($"  FAILED: no embedding");
            }

            await Task.Delay(1000); // Avoid rate limiting
        }

        Console.WriteLine($"\n=== Pipeline Results ===");
        Console.WriteLine($"Issues processed: {actualIssues.Count}");
        Console.WriteLine($"Embeddings succeeded: {successCount}");
        Console.WriteLine($"Embeddings failed: {failCount}");

        Assert.True(successCount > 0, "No embeddings were generated in the pipeline");
        Assert.True(failCount == 0, $"{failCount} embeddings failed - this will cause sync issues!");

        Console.WriteLine("\n=== PHASE 3 PASSED ===");
    }

    #endregion
}
