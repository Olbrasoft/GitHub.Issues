using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Business.Models.OpenAi;

/// <summary>
/// Request model for OpenAI-compatible chat completions API.
/// </summary>
internal class OpenAiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public OpenAiMessage[] Messages { get; set; } = [];

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}
