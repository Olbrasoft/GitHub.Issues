namespace Olbrasoft.GitHub.Issues.Data;

/// <summary>
/// Language codes matching .NET CultureInfo.LCID values.
/// Use instead of magic numbers for type safety and readability.
/// </summary>
public enum LanguageCode
{
    /// <summary>Czech (Czechia) - čeština (Česko)</summary>
    CsCZ = 1029,

    /// <summary>German (Germany) - Deutsch (Deutschland)</summary>
    DeDE = 1031,

    /// <summary>English (United States)</summary>
    EnUS = 1033
}

/// <summary>
/// Extension methods for LanguageCode enum.
/// </summary>
public static class LanguageCodeExtensions
{
    /// <summary>
    /// Checks if the language ID represents English (US).
    /// </summary>
    public static bool IsEnglish(this int languageId)
        => languageId == (int)LanguageCode.EnUS;

    /// <summary>
    /// Checks if the language ID represents Czech.
    /// </summary>
    public static bool IsCzech(this int languageId)
        => languageId == (int)LanguageCode.CsCZ;

    /// <summary>
    /// Checks if the language ID represents German.
    /// </summary>
    public static bool IsGerman(this int languageId)
        => languageId == (int)LanguageCode.DeDE;

    /// <summary>
    /// Converts an integer language ID to LanguageCode enum.
    /// </summary>
    public static LanguageCode ToLanguageCode(this int languageId)
        => (LanguageCode)languageId;

    /// <summary>
    /// Gets the integer LCID value from LanguageCode.
    /// </summary>
    public static int ToLcid(this LanguageCode languageCode)
        => (int)languageCode;
}
