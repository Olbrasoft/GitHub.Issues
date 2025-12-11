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

    /// <summary>
    /// Creates combined text for embedding from title, body, labels, and comments.
    /// Includes all searchable content from an issue.
    /// </summary>
    /// <param name="title">Issue title</param>
    /// <param name="body">Issue body/description</param>
    /// <param name="labelNames">List of label names</param>
    /// <param name="comments">List of comment bodies (in chronological order)</param>
    /// <returns>Combined text for embedding, truncated if necessary</returns>
    string CreateEmbeddingText(string title, string? body, IReadOnlyList<string>? labelNames, IReadOnlyList<string>? comments);
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
        return CreateEmbeddingText(title, body, null, null);
    }

    public string CreateEmbeddingText(string title, string? body, IReadOnlyList<string>? labelNames, IReadOnlyList<string>? comments)
    {
        var parts = new List<string> { title };

        // Add body if present
        if (!string.IsNullOrWhiteSpace(body))
        {
            parts.Add(body);
        }

        // Add labels if present
        if (labelNames is { Count: > 0 })
        {
            parts.Add($"Labels: {string.Join(", ", labelNames)}");
        }

        // Add comments if present
        if (comments is { Count: > 0 })
        {
            parts.Add("Comments:");
            foreach (var comment in comments)
            {
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    parts.Add("---");
                    parts.Add(comment);
                }
            }
        }

        var combined = string.Join("\n\n", parts);

        if (combined.Length > _maxLength)
        {
            return combined[.._maxLength];
        }

        return combined;
    }
}
