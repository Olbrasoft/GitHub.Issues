namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

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
