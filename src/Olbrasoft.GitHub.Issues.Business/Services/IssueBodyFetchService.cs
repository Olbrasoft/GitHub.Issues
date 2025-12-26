using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue body content from GitHub GraphQL API.
/// Single responsibility: GitHub API interaction only (SRP).
/// Refactored from IssueDetailService to follow Single Responsibility Principle.
/// </summary>
public class IssueBodyFetchService : IIssueBodyFetchService
{
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IIssueDetailQueryService _queryService;
    private readonly IBodyNotifier _bodyNotifier;
    private readonly IBodyPreviewGenerator _previewGenerator;
    private readonly BodyPreviewSettings _bodyPreviewSettings;
    private readonly ILogger<IssueBodyFetchService> _logger;

    public IssueBodyFetchService(
        IGitHubGraphQLClient graphQLClient,
        IIssueDetailQueryService queryService,
        IBodyNotifier bodyNotifier,
        IBodyPreviewGenerator previewGenerator,
        IOptions<BodyPreviewSettings> bodyPreviewSettings,
        ILogger<IssueBodyFetchService> logger)
    {
        ArgumentNullException.ThrowIfNull(graphQLClient);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(bodyNotifier);
        ArgumentNullException.ThrowIfNull(previewGenerator);
        ArgumentNullException.ThrowIfNull(bodyPreviewSettings);
        ArgumentNullException.ThrowIfNull(logger);

        _graphQLClient = graphQLClient;
        _queryService = queryService;
        _bodyNotifier = bodyNotifier;
        _previewGenerator = previewGenerator;
        _bodyPreviewSettings = bodyPreviewSettings.Value;
        _logger = logger;
    }

    public async Task FetchBodiesAsync(IEnumerable<int> issueIds, CancellationToken cancellationToken = default)
    {
        var idList = issueIds.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("[FetchBodies] START for {Count} issues: {Ids}", idList.Count, string.Join(", ", idList));

        // Load issues with repository info
        var issues = await _queryService.GetIssuesByIdsAsync(idList, cancellationToken);

        if (issues.Count == 0)
        {
            _logger.LogWarning("[FetchBodies] No issues found for IDs: {Ids}", string.Join(", ", idList));
            return;
        }

        // Build GraphQL requests
        var requests = issues
            .Select(i =>
            {
                var (owner, repo) = ParseRepositoryFullName(i.Repository.FullName);
                return new IssueBodyRequest(owner, repo, i.Number);
            })
            .Where(r => !string.IsNullOrEmpty(r.Owner) && !string.IsNullOrEmpty(r.Repo))
            .ToList();

        if (requests.Count == 0)
        {
            _logger.LogWarning("[FetchBodies] No valid requests after parsing repository names");
            return;
        }

        _logger.LogInformation("[FetchBodies] Fetching {Count} bodies from GitHub GraphQL", requests.Count);

        // Batch fetch from GraphQL
        var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

        _logger.LogInformation("[FetchBodies] Received {Count} bodies from GraphQL", bodies.Count);

        // Send notifications for each body
        foreach (var issue in issues)
        {
            var (owner, repo) = ParseRepositoryFullName(issue.Repository.FullName);
            var key = (owner, repo, issue.Number);

            if (bodies.TryGetValue(key, out var body) && !string.IsNullOrWhiteSpace(body))
            {
                var preview = _previewGenerator.CreatePreview(body, _bodyPreviewSettings.MaxLength);
                await _bodyNotifier.NotifyBodyReceivedAsync(
                    new BodyNotificationDto(issue.Id, preview),
                    cancellationToken);

                _logger.LogDebug("[FetchBodies] Sent body preview for issue {Id}", issue.Id);
            }
            else
            {
                _logger.LogDebug("[FetchBodies] No body found for issue {Id}", issue.Id);
            }
        }

        _logger.LogInformation("[FetchBodies] COMPLETE for {Count} issues", idList.Count);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
