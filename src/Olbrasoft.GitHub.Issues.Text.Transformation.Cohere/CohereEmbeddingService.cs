using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Text.Transformation.Cohere;

/// <summary>
/// Cohere cloud API embedding service implementation with dual API key rotation.
/// Uses embed-multilingual-v3.0 model which supports Czech and cross-language search.
/// Implements round-robin key rotation to maximize free tier quota.
/// </summary>
public class CohereEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<CohereEmbeddingService> _logger;
    private readonly IReadOnlyList<string> _apiKeys;

    // Thread-safe counter for round-robin key rotation
    private long _requestCounter;

    private const string CohereApiUrl = "https://api.cohere.com/v2/embed";

    public CohereEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingSettings> settings,
        ILogger<CohereEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _apiKeys = _settings.GetCohereApiKeys();

        if (_apiKeys.Count > 0)
        {
            _logger.LogInformation("Cohere embedding service initialized with {KeyCount} API key(s)", _apiKeys.Count);
        }
    }

    public bool IsConfigured => _apiKeys.Count > 0;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            var embedding = await GenerateEmbeddingAsync("test", EmbeddingInputType.Query, cancellationToken);
            return embedding != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text, EmbeddingInputType inputType = EmbeddingInputType.Document, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsConfigured)
        {
            _logger.LogWarning("Cohere API keys are not configured");
            return null;
        }

        var inputTypeString = inputType == EmbeddingInputType.Query ? "search_query" : "search_document";

        // Try with round-robin key, then retry with next key on 429
        var startKeyIndex = GetNextKeyIndex();
        var triedKeys = new HashSet<int>();

        for (var attempt = 0; attempt < _apiKeys.Count; attempt++)
        {
            var keyIndex = (startKeyIndex + attempt) % _apiKeys.Count;
            if (triedKeys.Contains(keyIndex))
            {
                break;
            }
            triedKeys.Add(keyIndex);

            var apiKey = _apiKeys[keyIndex];
            var maskedKey = MaskApiKey(apiKey);

            var result = await TryGenerateEmbeddingAsync(text, inputTypeString, apiKey, maskedKey, cancellationToken);

            if (result.Success)
            {
                return result.Embedding;
            }

            // Only retry with different key on rate limit (429)
            if (result.StatusCode != 429)
            {
                return null;
            }

            if (attempt < _apiKeys.Count - 1)
            {
                var nextKeyIndex = (keyIndex + 1) % _apiKeys.Count;
                var nextMaskedKey = MaskApiKey(_apiKeys[nextKeyIndex]);
                _logger.LogWarning("Cohere rate limit: key=...{MaskedKey}, switching to ...{NextMaskedKey}",
                    maskedKey, nextMaskedKey);
            }
        }

        _logger.LogError("All Cohere API keys exhausted (rate limited)");
        return null;
    }

    private async Task<(bool Success, float[]? Embedding, int StatusCode)> TryGenerateEmbeddingAsync(
        string text,
        string inputType,
        string apiKey,
        string maskedKey,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new CohereEmbedRequest
            {
                Texts = [text],
                Model = _settings.CohereModel,
                InputType = inputType,
                EmbeddingTypes = ["float"]
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CohereApiUrl);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (statusCode == 429)
                {
                    _logger.LogWarning("Cohere rate limit: key=...{MaskedKey}, status={StatusCode}, latency={Latency}ms",
                        maskedKey, statusCode, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Cohere failed: key=...{MaskedKey}, status={StatusCode}, latency={Latency}ms, error={Error}",
                        maskedKey, statusCode, stopwatch.ElapsedMilliseconds, errorContent);
                }

                return (false, null, statusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<CohereEmbedResponse>(cancellationToken);

            if (result?.Embeddings?.Float?.Count > 0)
            {
                var embedding = result.Embeddings.Float[0].ToArray();

                _logger.LogInformation("Cohere embed: key=...{MaskedKey}, texts=1, type={InputType}, status={StatusCode}, latency={Latency}ms",
                    maskedKey, inputType, statusCode, stopwatch.ElapsedMilliseconds);

                return (true, embedding, statusCode);
            }

            _logger.LogWarning("Cohere response did not contain embeddings: key=...{MaskedKey}", maskedKey);
            return (false, null, statusCode);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Cohere connection failed: key=...{MaskedKey}, latency={Latency}ms",
                maskedKey, stopwatch.ElapsedMilliseconds);
            return (false, null, 0);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Cohere error: key=...{MaskedKey}, latency={Latency}ms",
                maskedKey, stopwatch.ElapsedMilliseconds);
            return (false, null, 0);
        }
    }

    /// <summary>
    /// Gets the next API key index using round-robin rotation.
    /// Thread-safe using Interlocked.Increment.
    /// </summary>
    private int GetNextKeyIndex()
    {
        var count = Interlocked.Increment(ref _requestCounter);
        return (int)((count - 1) % _apiKeys.Count);
    }

    /// <summary>
    /// Masks API key for logging - shows only last 4 characters.
    /// </summary>
    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 4)
        {
            return "****";
        }
        return apiKey[^4..];
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
