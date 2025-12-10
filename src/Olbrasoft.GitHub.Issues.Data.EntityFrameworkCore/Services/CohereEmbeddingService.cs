using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Cohere cloud API embedding service implementation.
/// Uses embed-multilingual-v3.0 model which supports Czech and cross-language search.
/// </summary>
public class CohereEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<CohereEmbeddingService> _logger;

    private const string CohereApiUrl = "https://api.cohere.com/v2/embed";

    public CohereEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingSettings> settings,
        ILogger<CohereEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        // Set up authorization header
        if (!string.IsNullOrEmpty(_settings.CohereApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.CohereApiKey);
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settings.CohereApiKey);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            // Simple health check - try to get a small embedding
            var embedding = await GenerateEmbeddingAsync("test", EmbeddingInputType.Query, cancellationToken);
            return embedding != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Vector?> GenerateEmbeddingAsync(string text, EmbeddingInputType inputType = EmbeddingInputType.Document, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsConfigured)
        {
            _logger.LogWarning("Cohere API key is not configured");
            return null;
        }

        try
        {
            var request = new CohereEmbedRequest
            {
                Texts = [text],
                Model = _settings.CohereModel,
                InputType = inputType == EmbeddingInputType.Query ? "search_query" : "search_document",
                EmbeddingTypes = ["float"]
            };

            var response = await _httpClient.PostAsJsonAsync(CohereApiUrl, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Cohere API returned {StatusCode}: {Error}", response.StatusCode, errorContent);

                // Handle rate limiting
                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning("Cohere API rate limit exceeded. Consider upgrading from trial tier.");
                }

                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<CohereEmbedResponse>(cancellationToken);

            if (result?.Embeddings?.Float?.Count > 0)
            {
                var floats = result.Embeddings.Float[0];
                return new Vector(floats.ToArray());
            }

            _logger.LogWarning("Cohere response did not contain embeddings");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Cohere API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding via Cohere");
            return null;
        }
    }

    // Request/response DTOs for Cohere API v2

    private class CohereEmbedRequest
    {
        [JsonPropertyName("texts")]
        public string[] Texts { get; set; } = [];

        [JsonPropertyName("model")]
        public string Model { get; set; } = "embed-multilingual-v3.0";

        [JsonPropertyName("input_type")]
        public string InputType { get; set; } = "search_document";

        [JsonPropertyName("embedding_types")]
        public string[] EmbeddingTypes { get; set; } = ["float"];
    }

    private class CohereEmbedResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("embeddings")]
        public CohereEmbeddings? Embeddings { get; set; }
    }

    private class CohereEmbeddings
    {
        [JsonPropertyName("float")]
        public List<List<float>>? Float { get; set; }
    }
}
