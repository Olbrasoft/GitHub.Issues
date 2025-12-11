namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Settings for AI providers used in summarization and translation.
/// </summary>
public class AiProvidersSettings
{
    public AiProviderConfig Cerebras { get; set; } = new();
    public AiProviderConfig Groq { get; set; } = new();
    public CohereProviderConfig Cohere { get; set; } = new();
}

/// <summary>
/// Configuration for Cohere provider (uses different API format).
/// </summary>
public class CohereProviderConfig
{
    /// <summary>API endpoint URL.</summary>
    public string Endpoint { get; set; } = "https://api.cohere.com/v2/";

    /// <summary>Models to use for translation, in priority order.</summary>
    public string[] TranslationModels { get; set; } = ["command-a-translate-08-2025", "c4ai-aya-expanse-32b"];

    /// <summary>API keys to rotate through.</summary>
    public string[] Keys { get; set; } = [];
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

/// <summary>
/// Settings for translation behavior.
/// </summary>
public class TranslationSettings
{
    /// <summary>Maximum tokens in translation response.</summary>
    public int MaxTokens { get; set; } = 300;

    /// <summary>Temperature for generation (lower = more deterministic).</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Target language for translation.</summary>
    public string TargetLanguage { get; set; } = "Czech";

    /// <summary>System prompt for translation (for OpenAI-compatible providers).</summary>
    public string SystemPrompt { get; set; } = "Translate the following text to Czech. Preserve the meaning and tone. Output only the translation, nothing else.";
}
