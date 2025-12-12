using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Business.Models.OpenAi;

/// <summary>
/// Response model from OpenAI-compatible chat completions API.
/// </summary>
internal class OpenAiResponse
{
    [JsonPropertyName("choices")]
    public OpenAiChoice[]? Choices { get; set; }
}
