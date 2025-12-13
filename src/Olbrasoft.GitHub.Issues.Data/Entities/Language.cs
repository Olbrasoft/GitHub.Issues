using System.Globalization;

namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Language entity mapping to .NET CultureInfo.
/// Uses LCID (Locale Code Identifier) as primary key.
/// Use CultureInfo.GetCultureInfo(Id) to get full culture details (EnglishName, NativeName, etc.).
/// </summary>
public class Language
{
    /// <summary>
    /// Primary key - Windows LCID (Locale Code Identifier).
    /// NOT auto-generated, matches CultureInfo.LCID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Culture name for readability in DB (e.g., "cs-CZ", "en-US").
    /// </summary>
    public string CultureName { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property for cached texts in this language.
    /// </summary>
    public ICollection<CachedText> CachedTexts { get; set; } = [];

    /// <summary>
    /// Gets CultureInfo for this language from the system.
    /// </summary>
    public CultureInfo GetCultureInfo() => CultureInfo.GetCultureInfo(Id);
}
