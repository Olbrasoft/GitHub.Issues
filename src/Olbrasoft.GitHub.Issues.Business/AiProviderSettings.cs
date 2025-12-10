namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Settings for AI providers used in summarization.
/// </summary>
public class AiProvidersSettings
{
    public AiProviderConfig Cerebras { get; set; } = new();
    public AiProviderConfig Groq { get; set; } = new();
}

/// <summary>
/// Configuration for a single AI provider.
/// </summary>
public class AiProviderConfig
{
    /// <summary>API endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Models to use, in priority order.</summary>
    public string[] Models { get; set; } = [];

    /// <summary>API keys to rotate through. Store in User Secrets!</summary>
    public string[] Keys { get; set; } = [];
}

/// <summary>
/// Settings for summarization behavior.
/// </summary>
public class SummarizationSettings
{
    /// <summary>Maximum tokens in summary response.</summary>
    public int MaxTokens { get; set; } = 150;

    /// <summary>Temperature for generation (lower = more deterministic).</summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>System prompt for summarization.</summary>
    public string SystemPrompt { get; set; } = "You are a helpful assistant that summarizes GitHub issues concisely. Provide a 2-3 sentence summary in English that captures the key points.";
}
