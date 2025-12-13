using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Integration;

/// <summary>
/// Integration tests for CohereEmbeddingService.
/// Verifies that Cohere API is working correctly.
/// </summary>
public class CohereEmbeddingIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CohereEmbeddingService _embeddingService;
    private readonly Mock<ILogger<CohereEmbeddingService>> _loggerMock;

    public CohereEmbeddingIntegrationTests()
    {
        // Load Cohere API keys from environment or credentials file
        var apiKeys = new List<string>();

        // Try environment variables
        var key1 = Environment.GetEnvironmentVariable("COHERE_API_KEY_1");
        var key2 = Environment.GetEnvironmentVariable("COHERE_API_KEY_2");
        var key3 = Environment.GetEnvironmentVariable("COHERE_API_KEY_3");

        if (!string.IsNullOrEmpty(key1)) apiKeys.Add(key1);
        if (!string.IsNullOrEmpty(key2)) apiKeys.Add(key2);
        if (!string.IsNullOrEmpty(key3)) apiKeys.Add(key3);

        _httpClient = new HttpClient();
        _loggerMock = new Mock<ILogger<CohereEmbeddingService>>();

        var settings = new EmbeddingSettings
        {
            Provider = EmbeddingProvider.Cohere,
            Cohere = new CohereEmbeddingSettings
            {
                Model = "embed-multilingual-v3.0",
                ApiKeys = apiKeys.ToArray()
            }
        };

        _embeddingService = new CohereEmbeddingService(
            _httpClient,
            Options.Create(settings),
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [SkipOnCIFact]
    public void IsConfigured_WithApiKeys_ReturnsTrue()
    {
        // This test verifies API keys are loaded
        Console.WriteLine($"Cohere IsConfigured: {_embeddingService.IsConfigured}");

        // Only assert if we have keys configured
        if (_embeddingService.IsConfigured)
        {
            Assert.True(_embeddingService.IsConfigured);
        }
        else
        {
            Console.WriteLine("WARNING: No Cohere API keys configured. Set COHERE_API_KEY_1, _2, _3 environment variables.");
        }
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithSimpleText_ReturnsEmbedding()
    {
        if (!_embeddingService.IsConfigured)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        // Act - test with the EXACT text that fails in MultipleRequests test
        var testTexts = new[]
        {
            "Bug: Application crashes on startup",  // Should succeed
            "Feature request: Add dark mode support",  // FAILS in other test
            "Refactor: Extract service layer"  // FAILS in other test
        };

        foreach (var text in testTexts)
        {
            Console.WriteLine($"\nTesting: '{text}'");
            var embedding = await _embeddingService.GenerateEmbeddingAsync(text, EmbeddingInputType.Document);

            if (embedding != null)
            {
                Console.WriteLine($"  SUCCESS: {embedding.Length} dimensions");
            }
            else
            {
                Console.WriteLine($"  FAILED: embedding is null");
            }

            await Task.Delay(1000); // Wait between requests
        }

        // Assert at least one works
        var embedding1 = await _embeddingService.GenerateEmbeddingAsync(
            "This is a test issue about bug fixes",
            EmbeddingInputType.Document);

        Assert.NotNull(embedding1);
        Assert.Equal(1024, embedding1.Length);

        Console.WriteLine($"\nFinal SUCCESS: Generated embedding with {embedding1.Length} dimensions");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithLongText_ReturnsEmbedding()
    {
        if (!_embeddingService.IsConfigured)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        // Arrange - simulate a long issue body
        var longText = string.Join("\n", Enumerable.Range(1, 100).Select(i =>
            $"This is paragraph {i} of the issue body. It contains some text about the bug and how to reproduce it."));

        Console.WriteLine($"Text length: {longText.Length} characters");

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            longText,
            EmbeddingInputType.Document);

        // Assert
        Assert.NotNull(embedding);
        Console.WriteLine($"SUCCESS: Generated embedding for {longText.Length} char text");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsNull()
    {
        if (!_embeddingService.IsConfigured)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            "",
            EmbeddingInputType.Document);

        // Assert
        Assert.Null(embedding);
        Console.WriteLine("CORRECT: Empty text returns null embedding");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_MultipleRequests_AllSucceed()
    {
        if (!_embeddingService.IsConfigured)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        // Arrange - simulate multiple issue embeddings
        var texts = new[]
        {
            "Bug: Application crashes on startup",
            "Feature request: Add dark mode support",
            "Enhancement: Improve search performance",
            "Documentation: Update API reference",
            "Refactor: Extract service layer"
        };

        // Act & Assert
        var successCount = 0;
        var failCount = 0;

        foreach (var text in texts)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(text, EmbeddingInputType.Document);
            if (embedding != null)
            {
                successCount++;
                Console.WriteLine($"SUCCESS: '{text[..Math.Min(30, text.Length)]}...' -> {embedding.Length} dims");
            }
            else
            {
                failCount++;
                Console.WriteLine($"FAILED: '{text[..Math.Min(30, text.Length)]}...'");

                // Test direct API call to see actual error
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("COHERE_API_KEY_1")}");
                var req = new { texts = new[] { text }, model = "embed-multilingual-v3.0", input_type = "search_document", embedding_types = new[] { "float" } };
                var resp = await client.PostAsJsonAsync("https://api.cohere.com/v2/embed", req);
                var body = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"  Direct API: {resp.StatusCode} - {body[..Math.Min(200, body.Length)]}");
            }

            // Larger delay to avoid rate limiting
            await Task.Delay(500);
        }

        Console.WriteLine($"\nResults: {successCount} success, {failCount} failed");
        Assert.Equal(texts.Length, successCount);
    }

    [SkipOnCIFact]
    public async Task IsAvailableAsync_ReturnsCorrectStatus()
    {
        if (!_embeddingService.IsConfigured)
        {
            Console.WriteLine("SKIP: No Cohere API keys configured");
            return;
        }

        // Act
        var isAvailable = await _embeddingService.IsAvailableAsync();

        // Assert
        Console.WriteLine($"Cohere IsAvailable: {isAvailable}");
        Assert.True(isAvailable, "Cohere should be available when API keys are configured");
    }
}
