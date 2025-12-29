using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;
using Xunit;
using Xunit.Abstractions;

namespace Olbrasoft.GitHub.Issues.IntegrationTests;

/// <summary>
/// Integration tests for Cohere embedding service.
/// These tests call real Cohere API and are skipped on CI environments.
/// </summary>
public class CohereEmbeddingIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;

    public CohereEmbeddingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Load API key from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CohereEmbeddingIntegrationTests>()
            .AddEnvironmentVariables()
            .Build();

        _apiKey = configuration["Embeddings:CohereApiKeys:0"]
            ?? Environment.GetEnvironmentVariable("COHERE_API_KEY")
            ?? throw new InvalidOperationException(
                "Cohere API key not found. Set via user secrets or COHERE_API_KEY env var.");

        // Configure settings
        var settings = new EmbeddingSettings
        {
            Provider = EmbeddingProvider.Cohere,
            Model = "embed-multilingual-v3.0",
            Dimensions = 1024
        };
        settings.Cohere.ApiKeys = [_apiKey];
        settings.Cohere.Model = "embed-multilingual-v3.0";

        var options = Options.Create(settings);

        // Create logger
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = _loggerFactory.CreateLogger<CohereEmbeddingService>();

        // Create service
        _httpClient = new HttpClient();
        _embeddingService = new CohereEmbeddingService(_httpClient, options, logger);
    }

    [SkipOnCIFact]
    public void IsConfigured_WithValidApiKey_ReturnsTrue()
    {
        // Act & Assert
        Assert.True(_embeddingService.IsConfigured, "Service should be configured with valid API key");

        _output.WriteLine($"API Key (masked): ...{_apiKey[^4..]}");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithShortTitle_ReturnsValidEmbedding()
    {
        // Arrange
        var title = "Release";
        var textToEmbed = $"Title: {title}";

        _output.WriteLine($"Text to embed: {textToEmbed}");
        _output.WriteLine($"Text length: {textToEmbed.Length} chars");

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            textToEmbed,
            EmbeddingInputType.Document);

        // Assert
        Assert.NotNull(embedding);
        Assert.Equal(1024, embedding.Length); // Expected dimensions
        Assert.All(embedding, value => Assert.InRange(value, -1, 1)); // Normalized values

        _output.WriteLine($"SUCCESS! Embedding generated: {embedding.Length} dimensions");
        _output.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}...]");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithTitleAndBody_ReturnsValidEmbedding()
    {
        // Arrange
        var title = "Add Cohere embedding support";
        var body = "Add support for Cohere embeddings as an alternative to Ollama for cloud deployments.";
        var textToEmbed = $"Title: {title}\n\nBody: {body}";

        _output.WriteLine($"Text to embed: {textToEmbed}");
        _output.WriteLine($"Text length: {textToEmbed.Length} chars");

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            textToEmbed,
            EmbeddingInputType.Document);

        // Assert
        Assert.NotNull(embedding);
        Assert.Equal(1024, embedding.Length);
        Assert.All(embedding, value => Assert.InRange(value, -1, 1));

        _output.WriteLine($"SUCCESS! Embedding generated: {embedding.Length} dimensions");
        _output.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}...]");
    }

    [SkipOnCIFact]
    public async Task GenerateEmbeddingAsync_WithMultipleIssues_AllSucceed()
    {
        // Arrange
        var testIssues = new[]
        {
            new { Number = 228, Title = "Release", Body = "" },
            new { Number = 221, Title = "Search returns 0 results after Azure deployment",
                  Body = "After deploying to Azure, the issue search returns 0 results even though there are issues in the database." },
            new { Number = 220, Title = "Add Cohere embedding support",
                  Body = "Add support for Cohere embeddings as an alternative to Ollama for cloud deployments." },
            new { Number = 210, Title = "Don't use Cohere for translations",
                  Body = "Cohere should only be used for embeddings, not translations. Use proper translation APIs instead." }
        };

        // Act & Assert
        foreach (var issue in testIssues)
        {
            _output.WriteLine($"--- Issue #{issue.Number}: {issue.Title} ---");

            var textToEmbed = $"Title: {issue.Title}";
            if (!string.IsNullOrEmpty(issue.Body))
            {
                textToEmbed += $"\n\nBody: {issue.Body}";
            }

            _output.WriteLine($"Text length: {textToEmbed.Length} chars");

            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                textToEmbed,
                EmbeddingInputType.Document);

            Assert.NotNull(embedding);
            Assert.Equal(1024, embedding.Length);

            _output.WriteLine($"SUCCESS! Embedding: {embedding.Length} dimensions");
            _output.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}...]");
            _output.WriteLine("");
        }
    }

    [SkipOnCIFact]
    public async Task IsAvailableAsync_WithValidConfiguration_ReturnsTrue()
    {
        // Act
        var isAvailable = await _embeddingService.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable, "Service should be available with valid API key");

        _output.WriteLine($"IsAvailable: {isAvailable}");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _loggerFactory?.Dispose();
    }
}
