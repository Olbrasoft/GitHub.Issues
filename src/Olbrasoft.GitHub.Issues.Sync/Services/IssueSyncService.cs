using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing issues from GitHub.
/// </summary>
public class IssueSyncService : IIssueSyncService
{
    private readonly IIssueSyncBusinessService _issueSyncBusiness;
    private readonly IEmbeddingService _embeddingService;
    private readonly HttpClient _httpClient;
    private readonly SyncSettings _syncSettings;
    private readonly ILogger<IssueSyncService> _logger;

    public IssueSyncService(
        IIssueSyncBusinessService issueSyncBusiness,
        IEmbeddingService embeddingService,
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        IOptions<SyncSettings> syncSettings,
        ILogger<IssueSyncService> logger)
    {
        _issueSyncBusiness = issueSyncBusiness;
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _syncSettings = syncSettings.Value;
        _logger = logger;

        // Configure HttpClient for GitHub API
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));

        if (!string.IsNullOrEmpty(settings.Value.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.Token);
        }
    }

    public async Task SyncIssuesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        // Use UTC and encode the + sign for URL safety
        var sinceParam = since.HasValue
            ? $"&since={Uri.EscapeDataString(since.Value.UtcDateTime.ToString("O"))}"
            : "";

        _logger.LogInformation("Syncing issues for {Owner}/{Repo} using bulk API ({Mode}{Since})",
            owner, repo,
            since.HasValue ? "incremental" : "full",
            since.HasValue ? $", since {since.Value:u}" : "");

        var syncedAt = DateTimeOffset.UtcNow;
        var allIssues = new List<JsonElement>();
        var page = 1;

        // Fetch issues using bulk API with pagination (with optional since parameter)
        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues?state=all&per_page={_syncSettings.GitHubApiPageSize}&page={page}{sinceParam}";
            _logger.LogDebug("Fetching issues page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageIssues = doc.RootElement.EnumerateArray().ToList();
            if (pageIssues.Count == 0)
            {
                break;
            }

            // Clone elements since JsonDocument will be disposed
            foreach (var issue in pageIssues)
            {
                allIssues.Add(issue.Clone());
            }

            _logger.LogDebug("Fetched {Count} issues on page {Page}", pageIssues.Count, page);

            if (pageIssues.Count < _syncSettings.GitHubApiPageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} {Mode} issues for {Owner}/{Repo}",
            allIssues.Count,
            since.HasValue ? "changed" : "total",
            owner, repo);

        // Get existing issues for this repository to detect changes
        var existingIssues = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repository.Id, cancellationToken);

        // Dictionary to track parent_issue_url -> child issue number for later FK update
        var parentChildRelationships = new Dictionary<int, string>(); // childNumber -> parentIssueUrl

        foreach (var ghIssue in allIssues)
        {
            // Skip pull requests (they have pull_request property)
            if (ghIssue.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            var issueNumber = ghIssue.GetProperty("number").GetInt32();
            var title = ghIssue.GetProperty("title").GetString() ?? "";
            var body = ghIssue.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString()
                : null;
            var state = ghIssue.GetProperty("state").GetString();
            var htmlUrl = ghIssue.GetProperty("html_url").GetString() ?? "";
            var updatedAt = ghIssue.GetProperty("updated_at").GetDateTimeOffset();

            // Extract parent_issue_url if present
            if (ghIssue.TryGetProperty("parent_issue_url", out var parentUrlElement) &&
                parentUrlElement.ValueKind == JsonValueKind.String)
            {
                var parentUrl = parentUrlElement.GetString();
                if (!string.IsNullOrEmpty(parentUrl))
                {
                    parentChildRelationships[issueNumber] = parentUrl;
                }
            }

            // Check if issue exists and has changed
            var existingIssue = existingIssues.GetValueOrDefault(issueNumber);
            var isNew = existingIssue == null;
            var hasChanged = !isNew && existingIssue!.GitHubUpdatedAt < updatedAt;

            // Generate embedding for new issues OR re-embed if issue has changed
            Pgvector.Vector? embedding = null;
            if (isNew || hasChanged)
            {
                var textToEmbed = CreateEmbeddingText(title, body);
                embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed, cancellationToken);
                if (embedding == null)
                {
                    throw new InvalidOperationException($"Failed to generate embedding for issue #{issueNumber}. Ollama may be unavailable.");
                }

                if (hasChanged)
                {
                    _logger.LogDebug("Re-embedded changed issue #{Number}: {Title}", issueNumber, title);
                }
            }

            // Save issue (creates or updates)
            var savedIssue = await _issueSyncBusiness.SaveIssueAsync(
                repository.Id,
                issueNumber,
                title,
                state == "open",
                htmlUrl,
                updatedAt,
                syncedAt,
                embedding,
                cancellationToken);

            // Sync labels
            if (ghIssue.TryGetProperty("labels", out var labelsElement))
            {
                var labelNames = new List<string>();
                foreach (var labelElement in labelsElement.EnumerateArray())
                {
                    if (labelElement.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            labelNames.Add(name);
                        }
                    }
                }

                await _issueSyncBusiness.SyncLabelsAsync(savedIssue.Id, repository.Id, labelNames, cancellationToken);
            }

            _logger.LogDebug("Synced issue #{Number}: {Title}", issueNumber, title);
        }

        // Update parent-child relationships after all issues are synced
        await UpdateParentChildRelationshipsAsync(repository.Id, parentChildRelationships, cancellationToken);
    }

    private async Task UpdateParentChildRelationshipsAsync(
        int repositoryId,
        Dictionary<int, string> parentChildRelationships,
        CancellationToken cancellationToken)
    {
        if (parentChildRelationships.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Updating {Count} parent-child relationships", parentChildRelationships.Count);

        // Get all issues for this repository
        var issuesByNumber = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repositoryId, cancellationToken);

        // Build childId -> parentId map
        var childToParentMap = new Dictionary<int, int?>();
        foreach (var (childNumber, parentUrl) in parentChildRelationships)
        {
            var parentNumber = ExtractIssueNumberFromUrl(parentUrl);
            if (parentNumber.HasValue &&
                issuesByNumber.TryGetValue(parentNumber.Value, out var parentIssue) &&
                issuesByNumber.TryGetValue(childNumber, out var childIssue))
            {
                if (childIssue.ParentIssueId != parentIssue.Id)
                {
                    childToParentMap[childIssue.Id] = parentIssue.Id;
                    _logger.LogDebug("Set parent of issue #{Child} to #{Parent}", childNumber, parentNumber.Value);
                }
            }
        }

        if (childToParentMap.Count > 0)
        {
            var updatedCount = await _issueSyncBusiness.BatchSetParentsAsync(childToParentMap, cancellationToken);
            _logger.LogInformation("Updated {Count} parent-child relationships", updatedCount);
        }
    }

    /// <summary>
    /// Extracts issue number from GitHub API URL (e.g., https://api.github.com/repos/owner/repo/issues/123)
    /// </summary>
    private static int? ExtractIssueNumberFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // URL format: https://api.github.com/repos/{owner}/{repo}/issues/{number}
        var lastSlashIndex = url.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < url.Length - 1)
        {
            var numberPart = url[(lastSlashIndex + 1)..];
            if (int.TryParse(numberPart, out var number))
            {
                return number;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates combined text for embedding from title and body.
    /// Truncates to avoid exceeding token limits (configurable via SyncSettings.MaxEmbeddingTextLength).
    /// </summary>
    private string CreateEmbeddingText(string title, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return title;
        }

        var combined = $"{title}\n\n{body}";

        if (combined.Length > _syncSettings.MaxEmbeddingTextLength)
        {
            return combined[.._syncSettings.MaxEmbeddingTextLength];
        }

        return combined;
    }
}
