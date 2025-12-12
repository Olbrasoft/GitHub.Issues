using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Business.Models.OpenAi;

/// <summary>
/// Represents a message in OpenAI-compatible API request/response.
/// </summary>
internal class OpenAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
