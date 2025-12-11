using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// AI translation service with provider rotation.
/// Primary: Cohere (specialized translation model)
/// Fallback: Cerebras/Groq (OpenAI-compatible with qwen models for Czech support)
/// </summary>
public class AiTranslationService : IAiTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly AiProvidersSettings _providers;
    private readonly TranslationSettings _translation;
    private readonly ILogger<AiTranslationService> _logger;

    // Static rotation state - persists across all instances (service can be transient/scoped)
    // Thread-safe with Interlocked
    private static int _cohereKeyIndex;
    private static int _fallbackIndex;

    // Pre-built fallback combinations (Cerebras/Groq with Czech-capable models)
    private readonly List<TranslationProvider> _fallbacks;

    public AiTranslationService(
        HttpClient httpClient,
        IOptions<AiProvidersSettings> providers,
        IOptions<TranslationSettings> translation,
        ILogger<AiTranslationService> logger)
    {
        _httpClient = httpClient;
        _providers = providers.Value;
        _translation = translation.Value;
        _logger = logger;

        _fallbacks = BuildFallbacks();
    }

    private List<TranslationProvider> BuildFallbacks()
    {
        var fallbacks = new List<TranslationProvider>();

        // Add Cerebras with Czech-capable models (qwen-3-32b has official Czech support)
        var cerebrasKeys = _providers.Cerebras.Keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        foreach (var key in cerebrasKeys)
        {
            // qwen-3-32b officially supports 119 languages including Czech
            fallbacks.Add(new TranslationProvider("Cerebras", _providers.Cerebras.Endpoint, key, "qwen-3-32b"));
        }

        // Add Groq with Czech-capable models
        var groqKeys = _providers.Groq.Keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        foreach (var key in groqKeys)
        {
            fallbacks.Add(new TranslationProvider("Groq", _providers.Groq.Endpoint, key, "qwen/qwen3-32b"));
        }

        _logger.LogInformation("Built {Count} fallback translation providers", fallbacks.Count);
        return fallbacks;
    }

    public async Task<TranslationResult> TranslateToCzechAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslationResult.Fail("No text to translate");
        }

        // Try Cohere first (specialized translation)
        var cohereResult = await TryCohereTranslationAsync(text, cancellationToken);
        if (cohereResult.Success)
        {
            return cohereResult;
        }

        // Fallback to OpenAI-compatible providers
        return await TryFallbackTranslationAsync(text, cancellationToken);
    }

    private async Task<TranslationResult> TryCohereTranslationAsync(string text, CancellationToken cancellationToken)
    {
        var cohereKeys = _providers.Cohere.Keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (cohereKeys.Count == 0)
        {
            _logger.LogDebug("No Cohere API keys configured");
            return TranslationResult.Fail("No Cohere keys");
        }

        var models = _providers.Cohere.TranslationModels;
        if (models.Length == 0)
        {
            models = ["command-a-translate-08-2025", "c4ai-aya-expanse-32b"];
        }

        // Try each key/model combination
        foreach (var model in models)
        {
            var keyIndex = Interlocked.Increment(ref _cohereKeyIndex) % cohereKeys.Count;
            var apiKey = cohereKeys[keyIndex];

            try
            {
                var result = await CallCohereAsync(apiKey, model, text, cancellationToken);
                if (result.Success)
                {
                    return result;
                }
                _logger.LogWarning("Cohere translation failed with {Model}: {Error}", model, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during Cohere translation with {Model}", model);
            }
        }

        return TranslationResult.Fail("All Cohere attempts failed");
    }

    private async Task<TranslationResult> CallCohereAsync(
        string apiKey,
        string model,
        string text,
        CancellationToken cancellationToken)
    {
        // Cohere v2 chat API format
        var request = new CohereRequest
        {
            Model = model,
            Messages =
            [
                new CohereMessage
                {
                    Role = "user",
                    Content = $"Translate the following text to Czech. Output only the translation, nothing else.\n\n{text}"
                }
            ]
        };

        var json = JsonSerializer.Serialize(request, TranslationJsonContext.Default.CohereRequest);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _providers.Cohere.Endpoint + "chat");
        httpRequest.Content = httpContent;
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        _logger.LogDebug("Trying Cohere translation with {Model}", model);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return TranslationResult.Fail($"HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var cohereResponse = JsonSerializer.Deserialize(responseBody, TranslationJsonContext.Default.CohereResponse);

        var translation = cohereResponse?.Message?.Content?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(translation))
        {
            return TranslationResult.Fail("Empty response from Cohere");
        }

        _logger.LogInformation("Translation successful with Cohere/{Model}", model);
        return TranslationResult.Ok(translation, "Cohere", model);
    }

    private async Task<TranslationResult> TryFallbackTranslationAsync(string text, CancellationToken cancellationToken)
    {
        if (_fallbacks.Count == 0)
        {
            return TranslationResult.Fail("No fallback providers configured");
        }

        var startIndex = Interlocked.Increment(ref _fallbackIndex) % _fallbacks.Count;
        var tried = 0;

        while (tried < _fallbacks.Count)
        {
            var index = (startIndex + tried) % _fallbacks.Count;
            var provider = _fallbacks[index];

            try
            {
                var result = await CallOpenAiCompatibleAsync(provider, text, cancellationToken);
                if (result.Success)
                {
                    return result;
                }
                _logger.LogWarning("Fallback translation failed with {Provider}/{Model}: {Error}",
                    provider.Name, provider.Model, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during fallback translation with {Provider}/{Model}",
                    provider.Name, provider.Model);
            }

            tried++;
        }

        return TranslationResult.Fail("All translation providers failed");
    }

    private async Task<TranslationResult> CallOpenAiCompatibleAsync(
        TranslationProvider provider,
        string text,
        CancellationToken cancellationToken)
    {
        var request = new OpenAiTranslationRequest
        {
            Model = provider.Model,
            Messages =
            [
                new OpenAiTranslationMessage { Role = "system", Content = _translation.SystemPrompt },
                new OpenAiTranslationMessage { Role = "user", Content = text }
            ],
            MaxTokens = _translation.MaxTokens,
            Temperature = _translation.Temperature
        };

        var json = JsonSerializer.Serialize(request, TranslationJsonContext.Default.OpenAiTranslationRequest);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, provider.Endpoint + "chat/completions");
        httpRequest.Content = httpContent;
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        _logger.LogDebug("Trying fallback translation with {Provider}/{Model}", provider.Name, provider.Model);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return TranslationResult.Fail($"HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var openAiResponse = JsonSerializer.Deserialize(responseBody, TranslationJsonContext.Default.OpenAiTranslationResponse);

        var translation = openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(translation))
        {
            return TranslationResult.Fail("Empty response from API");
        }

        // Strip <think>...</think> tags from chain-of-thought models
        translation = StripThinkingTags(translation);

        if (string.IsNullOrWhiteSpace(translation))
        {
            return TranslationResult.Fail("Response truncated (increase MaxTokens)");
        }

        _logger.LogInformation("Translation successful with {Provider}/{Model}", provider.Name, provider.Model);
        return TranslationResult.Ok(translation, provider.Name, provider.Model);
    }

    private static string StripThinkingTags(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = System.Text.RegularExpressions.Regex.Replace(
            content,
            @"<think>[\s\S]*?</think>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (result.TrimStart().StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return result.Trim();
    }

    private record TranslationProvider(string Name, string Endpoint, string ApiKey, string Model);
}

// Cohere API models
internal class CohereRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public CohereMessage[] Messages { get; set; } = [];
}

internal class CohereMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class CohereResponse
{
    [JsonPropertyName("message")]
    public CohereResponseMessage? Message { get; set; }
}

internal class CohereResponseMessage
{
    [JsonPropertyName("content")]
    public CohereContentItem[]? Content { get; set; }
}

internal class CohereContentItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// OpenAI-compatible models for fallback
internal class OpenAiTranslationRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public OpenAiTranslationMessage[] Messages { get; set; } = [];

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

internal class OpenAiTranslationMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class OpenAiTranslationResponse
{
    [JsonPropertyName("choices")]
    public OpenAiTranslationChoice[]? Choices { get; set; }
}

internal class OpenAiTranslationChoice
{
    [JsonPropertyName("message")]
    public OpenAiTranslationMessage? Message { get; set; }
}

[JsonSerializable(typeof(CohereRequest))]
[JsonSerializable(typeof(CohereResponse))]
[JsonSerializable(typeof(OpenAiTranslationRequest))]
[JsonSerializable(typeof(OpenAiTranslationResponse))]
internal partial class TranslationJsonContext : JsonSerializerContext
{
}
