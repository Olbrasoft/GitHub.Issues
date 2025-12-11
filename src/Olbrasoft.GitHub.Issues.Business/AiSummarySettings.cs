namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Configuration for AI-generated summaries on search results page.
/// </summary>
public class AiSummarySettings
{
    /// <summary>
    /// Maximum length of AI summary in characters. Default: 500
    /// </summary>
    public int MaxLength { get; set; } = 500;

    /// <summary>
    /// Default language selection in UI ComboBox: "en", "cs", or "both". Default: "en"
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// When "both" is selected, which language shows first: "en" or "cs". Default: "cs"
    /// </summary>
    public string PrimaryLanguage { get; set; } = "cs";
}
