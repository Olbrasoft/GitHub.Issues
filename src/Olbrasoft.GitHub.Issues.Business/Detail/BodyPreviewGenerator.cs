using System.Text.RegularExpressions;

namespace Olbrasoft.GitHub.Issues.Business.Detail;

/// <summary>
/// Generates text previews from markdown content.
/// Strips markdown formatting and truncates to specified length.
/// </summary>
public partial class BodyPreviewGenerator : IBodyPreviewGenerator
{
    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Multiline)]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"^#+\s+", RegexOptions.Multiline)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]+\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex ItalicPattern();

    [GeneratedRegex(@"__([^_]+)__")]
    private static partial Regex BoldUnderscorePattern();

    [GeneratedRegex(@"_([^_]+)_")]
    private static partial Regex ItalicUnderscorePattern();

    [GeneratedRegex(@"^>\s*", RegexOptions.Multiline)]
    private static partial Regex BlockquotePattern();

    [GeneratedRegex(@"^[-*_]{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    /// <inheritdoc />
    public string CreatePreview(string body, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var text = body;

        text = CodeBlockPattern().Replace(text, " ");
        text = InlineCodePattern().Replace(text, " ");
        text = HeaderPattern().Replace(text, "");
        text = ImagePattern().Replace(text, "");
        text = LinkPattern().Replace(text, "$1");
        text = BoldPattern().Replace(text, "$1");
        text = ItalicPattern().Replace(text, "$1");
        text = BoldUnderscorePattern().Replace(text, "$1");
        text = ItalicUnderscorePattern().Replace(text, "$1");
        text = BlockquotePattern().Replace(text, "");
        text = HorizontalRulePattern().Replace(text, "");
        text = WhitespacePattern().Replace(text, " ");
        text = text.Trim();

        if (text.Length <= maxLength)
        {
            return text;
        }

        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.7)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated + "...";
    }
}
