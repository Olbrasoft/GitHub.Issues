using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Business.Models.OpenAi;

/// <summary>
/// JSON serialization context for AOT compatibility with OpenAI models.
/// </summary>
[JsonSerializable(typeof(OpenAiRequest))]
[JsonSerializable(typeof(OpenAiResponse))]
internal partial class OpenAiJsonContext : JsonSerializerContext
{
}
