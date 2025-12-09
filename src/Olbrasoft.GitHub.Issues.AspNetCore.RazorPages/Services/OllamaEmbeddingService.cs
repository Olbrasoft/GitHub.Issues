using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingSettings> settings,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settings.BaseUrl);

    public async Task<Vector?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var request = new
            {
                model = _settings.Model,
                prompt = text
            };

            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            if (json.TryGetProperty("embedding", out var embeddingElement))
            {
                var floats = embeddingElement.EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                return new Vector(floats);
            }

            _logger.LogWarning("Ollama response did not contain embedding property");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama at {BaseUrl}", _settings.BaseUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return null;
        }
    }
}
