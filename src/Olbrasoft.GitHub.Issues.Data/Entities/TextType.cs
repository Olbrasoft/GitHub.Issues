namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Lookup table for types of translatable/cacheable text content.
/// </summary>
public class TextType
{
    /// <summary>
    /// Primary key - auto-generated.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the text type (e.g., "Title", "ListSummary", "DetailSummary").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property for cached texts of this type.
    /// </summary>
    public ICollection<CachedText> CachedTexts { get; set; } = [];
}
