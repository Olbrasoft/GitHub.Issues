using System.Text.RegularExpressions;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Generates text previews from markdown content.
/// Strips markdown formatting and truncates to specified length.
/// </summary>
public class BodyPreviewGenerator : IBodyPreviewGenerator
{
    /// <inheritdoc />
    public string CreatePreview(string body, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var text = body;

        // Remove code blocks
        text = Regex.Replace(text, @"```[\s\S]*?```", " ", RegexOptions.Multiline);
        text = Regex.Replace(text, @"`[^`]+`", " ");

        // Remove headers
        text = Regex.Replace(text, @"^#+\s+", "", RegexOptions.Multiline);

        // Remove links but keep text: [text](url) -> text
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");

        // Remove images: ![alt](url)
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]+\)", "");

        // Remove bold/italic markers
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        text = Regex.Replace(text, @"\*([^*]+)\*", "$1");
        text = Regex.Replace(text, @"__([^_]+)__", "$1");
        text = Regex.Replace(text, @"_([^_]+)_", "$1");

        // Remove blockquotes
        text = Regex.Replace(text, @"^>\s*", "", RegexOptions.Multiline);

        // Remove horizontal rules
        text = Regex.Replace(text, @"^[-*_]{3,}\s*$", "", RegexOptions.Multiline);

        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim();

        // Truncate if needed
        if (text.Length <= maxLength)
        {
            return text;
        }

        // Try to truncate at word boundary
        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.7)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated + "...";
    }
}
