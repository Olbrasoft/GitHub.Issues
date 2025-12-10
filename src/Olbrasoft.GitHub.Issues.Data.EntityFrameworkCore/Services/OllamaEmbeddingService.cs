using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Ollama-based embedding service implementation.
/// Implements both IEmbeddingService (core functionality) and IServiceLifecycleManager (Ollama startup).
/// Uses IServiceManager for testable service management (no direct Process.Start calls).
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService, IServiceLifecycleManager
{
    private readonly HttpClient _httpClient;
    private readonly IServiceManager _serviceManager;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    private const string OllamaServiceName = "ollama";

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IServiceManager serviceManager,
        IOptions<EmbeddingSettings> settings,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _serviceManager = serviceManager;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settings.BaseUrl);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        if (await IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Ollama is already running");
            return;
        }

        _logger.LogInformation("Ollama is not running. Starting Ollama service...");

        await _serviceManager.StartServiceAsync(OllamaServiceName, cancellationToken);

        // Wait for Ollama to be ready (retry loop with HTTP health check)
        const int maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            if (await IsAvailableAsync(cancellationToken))
            {
                _logger.LogInformation("Ollama started successfully");
                return;
            }
            await Task.Delay(1000, cancellationToken);
        }

        throw new InvalidOperationException("Failed to start Ollama after 30 seconds");
    }

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
