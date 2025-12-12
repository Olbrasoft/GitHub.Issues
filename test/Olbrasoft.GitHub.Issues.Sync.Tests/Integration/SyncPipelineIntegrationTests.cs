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
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set (required for this integration test)");
            return;
        }

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
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set (required for this integration test)");
            return;
        }

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
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set (required for this integration test)");
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY_1");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("SKIP: COHERE_API_KEY_1 not set");
            return;
        }

        Console.WriteLine("=== PHASE 3: Full Pipeline (GitHub + Cohere) ===\n");

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

    #region End-to-End GitHub Sync Test

    /// <summary>
    /// END-TO-END TEST: Create issue on GitHub, verify sync returns it, cleanup.
    /// This test verifies the complete sync pipeline works correctly.
    /// NO translations, NO embeddings - just GitHub API sync verification.
    /// </summary>
    [Fact]
    public async Task EndToEnd_CreateIssue_SyncReturnsIt_Cleanup()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            Console.WriteLine("SKIP: GITHUB_TOKEN not set");
            return;
        }

        Console.WriteLine("=== END-TO-END SYNC TEST ===\n");

        int? createdIssueNumber = null;

        try
        {
            // STEP 1: Create test issue on GitHub
            Console.WriteLine("STEP 1: Creating test issue on GitHub...");

            using var createRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.github.com/repos/{TestOwner}/{TestRepo}/issues");
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            createRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            createRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            createRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("Integration-Test", "1.0"));
            createRequest.Content = JsonContent.Create(new
            {
                title = $"[TEST] Sync integration test - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                body = "This is a test issue created by integration test. Will be deleted automatically.",
                labels = new[] { "test" }
            });

            using var createClient = new HttpClient();
            var createResponse = await createClient.SendAsync(createRequest);
            var createContent = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"  FAILED to create issue: {createResponse.StatusCode}");
                Console.WriteLine($"  Response: {createContent}");
                Assert.Fail("Failed to create test issue on GitHub");
                return;
            }

            using var createDoc = JsonDocument.Parse(createContent);
            createdIssueNumber = createDoc.RootElement.GetProperty("number").GetInt32();
            var createdTitle = createDoc.RootElement.GetProperty("title").GetString();

            Console.WriteLine($"  SUCCESS: Created issue #{createdIssueNumber}: {createdTitle}\n");

            // Wait a moment for GitHub to index
            await Task.Delay(2000);

            // STEP 2: Use our GitHubIssueApiClient to fetch issues
            Console.WriteLine("STEP 2: Fetching issues using GitHubIssueApiClient...");

            var syncSettings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
            var loggerMock = new Mock<ILogger<GitHubIssueApiClient>>();
            var apiClient = new GitHubIssueApiClient(_githubClient, syncSettings, loggerMock.Object);

            var issues = await apiClient.FetchIssuesAsync(TestOwner, TestRepo, since: null);

            Console.WriteLine($"  Fetched {issues.Count} items from GitHub\n");

            // STEP 3: Verify our new issue is in the results
            Console.WriteLine("STEP 3: Verifying new issue is in sync results...");

            var foundIssue = issues.FirstOrDefault(i => i.Number == createdIssueNumber);

            if (foundIssue != null)
            {
                Console.WriteLine($"  ✅ SUCCESS: Issue #{createdIssueNumber} found in sync results!");
                Console.WriteLine($"     Title: {foundIssue.Title}");
                Console.WriteLine($"     State: {foundIssue.State}");
                Console.WriteLine($"     IsPullRequest: {foundIssue.IsPullRequest}");
            }
            else
            {
                Console.WriteLine($"  ❌ FAIL: Issue #{createdIssueNumber} NOT FOUND in sync results!");
                Console.WriteLine($"  All issue numbers returned: {string.Join(", ", issues.Select(i => i.Number).OrderBy(n => n))}");
                Assert.Fail($"Sync did not return the newly created issue #{createdIssueNumber}");
            }

            Assert.NotNull(foundIssue);
            Assert.Equal(createdIssueNumber, foundIssue.Number);
        }
        finally
        {
            // STEP 4: Cleanup - close the test issue
            if (createdIssueNumber.HasValue)
            {
                Console.WriteLine($"\nSTEP 4: Cleaning up - closing issue #{createdIssueNumber}...");

                using var closeRequest = new HttpRequestMessage(HttpMethod.Patch,
                    $"https://api.github.com/repos/{TestOwner}/{TestRepo}/issues/{createdIssueNumber}");
                closeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
                closeRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                closeRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                closeRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("Integration-Test", "1.0"));
                closeRequest.Content = JsonContent.Create(new { state = "closed" });

                using var closeClient = new HttpClient();
                var closeResponse = await closeClient.SendAsync(closeRequest);

                if (closeResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  SUCCESS: Issue #{createdIssueNumber} closed.\n");
                }
                else
                {
                    Console.WriteLine($"  WARNING: Failed to close issue #{createdIssueNumber}: {closeResponse.StatusCode}\n");
                }
            }
        }

        Console.WriteLine("=== END-TO-END TEST PASSED ===");
    }

    #endregion
}
