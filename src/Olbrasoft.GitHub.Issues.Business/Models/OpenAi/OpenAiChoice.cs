using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Business.Models.OpenAi;

/// <summary>
/// Represents a choice in OpenAI-compatible API response.
/// </summary>
internal class OpenAiChoice
{
    [JsonPropertyName("message")]
    public OpenAiMessage? Message { get; set; }
}
