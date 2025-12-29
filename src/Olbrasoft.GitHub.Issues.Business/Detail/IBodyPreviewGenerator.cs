namespace Olbrasoft.GitHub.Issues.Business.Detail;

/// <summary>
/// Service for generating text previews from markdown content.
/// Single responsibility: Strip markdown and truncate text.
/// </summary>
public interface IBodyPreviewGenerator
{
    /// <summary>
    /// Creates a preview of markdown text by stripping formatting and truncating.
    /// </summary>
    /// <param name="body">The markdown body text</param>
    /// <param name="maxLength">Maximum length of the preview</param>
    /// <returns>Plain text preview</returns>
    string CreatePreview(string body, int maxLength);
}
