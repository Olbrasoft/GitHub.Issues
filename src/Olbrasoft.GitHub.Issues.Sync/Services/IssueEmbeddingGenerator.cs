using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Generates embeddings for GitHub issues.
/// Coordinates comment fetching, text building, and embedding generation.
/// </summary>
public class IssueEmbeddingGenerator : IIssueEmbeddingGenerator
{
    private readonly IGitHubIssueApiClient _apiClient;
    private readonly IEmbeddingTextBuilder _textBuilder;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<IssueEmbeddingGenerator> _logger;

    public IssueEmbeddingGenerator(
        IGitHubIssueApiClient apiClient,
        IEmbeddingTextBuilder textBuilder,
        IEmbeddingService embeddingService,
        ILogger<IssueEmbeddingGenerator> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _textBuilder = textBuilder ?? throw new ArgumentNullException(nameof(textBuilder));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<float[]?> GenerateEmbeddingAsync(
        string owner,
        string repo,
        int issueNumber,
        string title,
        string? body,
        IReadOnlyList<string> labelNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fetch comments from GitHub API
            var comments = await FetchCommentsAsync(owner, repo, issueNumber, cancellationToken);

            // Build text with title, body, labels, and comments
            var textToEmbed = _textBuilder.CreateEmbeddingText(title, body, labelNames, comments);

            // Generate embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                textToEmbed,
                EmbeddingInputType.Document,
                cancellationToken);

            if (embedding == null)
            {
                _logger.LogWarning("Failed to generate embedding for issue #{Number}", issueNumber);
            }
            else
            {
                _logger.LogDebug("Generated embedding for issue #{Number}: {Dimensions} dimensions",
                    issueNumber, embedding.Length);
            }

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for issue #{Number}", issueNumber);
            return null;
        }
    }

    private async Task<IReadOnlyList<string>?> FetchCommentsAsync(
        string owner,
        string repo,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var comments = await _apiClient.FetchIssueCommentsAsync(owner, repo, issueNumber, cancellationToken);
            _logger.LogDebug("Fetched {Count} comments for issue #{Number}", comments.Count, issueNumber);
            return comments;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch comments for issue #{Number}, embedding without comments", issueNumber);
            return null;
        }
    }
}
