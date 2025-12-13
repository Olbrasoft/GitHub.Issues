namespace Olbrasoft.GitHub.Issues.Data;

/// <summary>
/// Text type codes for type-safe access to cached text types.
/// Values match the seeded TextType.Id values in database.
/// </summary>
public enum TextTypeCode
{
    /// <summary>Issue title (translated for non-English languages)</summary>
    Title = 1,

    /// <summary>Short AI-generated summary for search results/listings</summary>
    ListSummary = 2,

    /// <summary>Detailed AI-generated summary for issue detail page</summary>
    DetailSummary = 3
}

/// <summary>
/// Extension methods for TextTypeCode enum.
/// </summary>
public static class TextTypeCodeExtensions
{
    /// <summary>
    /// Gets the integer ID value from TextTypeCode.
    /// </summary>
    public static int ToId(this TextTypeCode textType)
        => (int)textType;

    /// <summary>
    /// Converts an integer to TextTypeCode enum.
    /// </summary>
    public static TextTypeCode ToTextTypeCode(this int id)
        => (TextTypeCode)id;
}
