namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Builds text for embedding generation from issue content.
/// Single responsibility: Text preparation and truncation.
/// </summary>
public interface IEmbeddingTextBuilder
{
    /// <summary>
    /// Creates combined text for embedding from title and body.
    /// Truncates to avoid exceeding token limits.
    /// </summary>
    string CreateEmbeddingText(string title, string? body);
}

/// <summary>
/// Default implementation of IEmbeddingTextBuilder.
/// </summary>
public class EmbeddingTextBuilder : IEmbeddingTextBuilder
{
    private readonly int _maxLength;

    public EmbeddingTextBuilder(int maxLength = 8000)
    {
        _maxLength = maxLength;
    }

    public string CreateEmbeddingText(string title, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return title;
        }

        var combined = $"{title}\n\n{body}";

        if (combined.Length > _maxLength)
        {
            return combined[.._maxLength];
        }

        return combined;
    }
}
