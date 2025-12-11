namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

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
