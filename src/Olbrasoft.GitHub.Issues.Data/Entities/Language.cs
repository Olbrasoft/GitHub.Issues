namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Language entity mapping to .NET CultureInfo.
/// Uses LCID (Locale Code Identifier) as primary key.
/// </summary>
public class Language
{
    /// <summary>
    /// Primary key - Windows LCID (Locale Code Identifier).
    /// NOT auto-generated, matches CultureInfo.LCID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Culture name in format languagecode2-country/regioncode2 (e.g., cs-CZ, en-US).
    /// </summary>
    public string CultureName { get; set; } = string.Empty;

    /// <summary>
    /// Full name in English (e.g., "Czech (Czechia)").
    /// </summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>
    /// Full name in native language (e.g., "čeština (Česko)").
    /// </summary>
    public string? NativeName { get; set; }

    /// <summary>
    /// ISO 639-1 two-letter language code (e.g., "cs", "en", "de").
    /// </summary>
    public string? TwoLetterISOCode { get; set; }

    /// <summary>
    /// Navigation property for cached translations in this language.
    /// </summary>
    public ICollection<TranslatedText> TranslatedTexts { get; set; } = new List<TranslatedText>();
}
