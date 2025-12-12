using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;

Console.WriteLine("=== Cohere Embedding Integration Test ===\n");

// Load API key from user secrets
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var cohereApiKey = configuration["Embeddings:CohereApiKeys:0"]
    ?? Environment.GetEnvironmentVariable("COHERE_API_KEY")
    ?? throw new InvalidOperationException("Cohere API key not found. Set via user secrets or COHERE_API_KEY env var.");

// Configure settings
var settings = new EmbeddingSettings
{
    Provider = EmbeddingProvider.Cohere,
    Model = "embed-multilingual-v3.0",
    Dimensions = 1024
};
settings.Cohere.ApiKeys = [cohereApiKey];
settings.Cohere.Model = "embed-multilingual-v3.0";

var options = Options.Create(settings);

// Create logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
var logger = loggerFactory.CreateLogger<CohereEmbeddingService>();

// Create HTTP client and service
using var httpClient = new HttpClient();
var embeddingService = new CohereEmbeddingService(httpClient, options, logger);

Console.WriteLine($"IsConfigured: {embeddingService.IsConfigured}");
Console.WriteLine($"API Key (masked): ...{cohereApiKey[^4..]}");
Console.WriteLine();

// Test issues from GitHub - these are the ones that failed
var testIssues = new[]
{
    new { Number = 228, Title = "Release", Body = "" },
    new { Number = 221, Title = "Search returns 0 results after Azure deployment", Body = "After deploying to Azure, the issue search returns 0 results even though there are issues in the database." },
    new { Number = 220, Title = "Add Cohere embedding support", Body = "Add support for Cohere embeddings as an alternative to Ollama for cloud deployments." },
    new { Number = 210, Title = "Don't use Cohere for translations", Body = "Cohere should only be used for embeddings, not translations. Use proper translation APIs instead." }
};

Console.WriteLine("Testing embedding generation for specific issues:\n");

foreach (var issue in testIssues)
{
    Console.WriteLine($"--- Issue #{issue.Number}: {issue.Title} ---");

    // Build text to embed (similar to EmbeddingTextBuilder)
    var textToEmbed = $"Title: {issue.Title}";
    if (!string.IsNullOrEmpty(issue.Body))
    {
        textToEmbed += $"\n\nBody: {issue.Body}";
    }

    Console.WriteLine($"Text length: {textToEmbed.Length} chars");

    try
    {
        var embedding = await embeddingService.GenerateEmbeddingAsync(
            textToEmbed,
            EmbeddingInputType.Document);

        if (embedding != null)
        {
            Console.WriteLine($"SUCCESS! Embedding generated: {embedding.Length} dimensions");
            Console.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}...]");
        }
        else
        {
            Console.WriteLine("FAILED! Embedding is null.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
    }

    Console.WriteLine();
}

// Test availability
Console.WriteLine("--- Testing service availability ---");
var isAvailable = await embeddingService.IsAvailableAsync();
Console.WriteLine($"IsAvailable: {isAvailable}");

Console.WriteLine("\n=== Test Complete ===");
