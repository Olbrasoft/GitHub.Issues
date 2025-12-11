namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Data Transfer Object for issue labels.
/// Contains label name and color for display.
/// </summary>
public record LabelDto(string Name, string Color)
{
    /// <summary>
    /// Gets the contrast text color (black or white) based on background luminance.
    /// </summary>
    public string TextColor => GetContrastColor(Color);

    private static string GetContrastColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || hexColor.Length < 6)
            return "#000000";

        try
        {
            var r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
            var g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
            var b = Convert.ToInt32(hexColor.Substring(4, 2), 16);

            // Calculate luminance using perceived brightness formula
            var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

            return luminance > 0.5 ? "#000000" : "#ffffff";
        }
        catch
        {
            return "#000000";
        }
    }
}
