using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// GitHub REST API client for fetching issues.
/// Single responsibility: HTTP communication and JSON parsing.
/// </summary>
public class GitHubIssueApiClient : IGitHubIssueApiClient
{
    private readonly HttpClient _httpClient;
    private readonly int _pageSize;
    private readonly ILogger<GitHubIssueApiClient> _logger;

    public GitHubIssueApiClient(
        HttpClient httpClient,
        IOptions<SyncSettings> syncSettings,
        ILogger<GitHubIssueApiClient> logger)
    {
        _httpClient = httpClient;
        _pageSize = syncSettings.Value.GitHubApiPageSize;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GitHubIssueDto>> FetchIssuesAsync(
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var sinceParam = since.HasValue
            ? $"&since={Uri.EscapeDataString(since.Value.UtcDateTime.ToString("O"))}"
            : "";

        _logger.LogInformation("Fetching issues for {Owner}/{Repo} ({Mode}{Since})",
            owner, repo,
            since.HasValue ? "incremental" : "full",
            since.HasValue ? $", since {since.Value:u}" : "");

        var allIssues = new List<GitHubIssueDto>();
        var page = 1;

        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues?state=all&per_page={_pageSize}&page={page}{sinceParam}";
            _logger.LogDebug("Fetching issues page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageIssues = ParseIssuesFromJson(doc.RootElement);
            if (pageIssues.Count == 0)
            {
                break;
            }

            allIssues.AddRange(pageIssues);
            _logger.LogDebug("Fetched {Count} issues on page {Page}", pageIssues.Count, page);

            if (pageIssues.Count < _pageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} {Mode} issues for {Owner}/{Repo}",
            allIssues.Count,
            since.HasValue ? "changed" : "total",
            owner, repo);

        return allIssues;
    }

    public async Task<IReadOnlyList<string>> FetchIssueCommentsAsync(
        string owner,
        string repo,
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching comments for issue #{Number} in {Owner}/{Repo}", issueNumber, owner, repo);

        var allComments = new List<string>();
        var page = 1;

        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues/{issueNumber}/comments?per_page={_pageSize}&page={page}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageComments = ParseCommentsFromJson(doc.RootElement);
            if (pageComments.Count == 0)
            {
                break;
            }

            allComments.AddRange(pageComments);

            if (pageComments.Count < _pageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogDebug("Fetched {Count} comments for issue #{Number}", allComments.Count, issueNumber);
        return allComments;
    }

    private static List<string> ParseCommentsFromJson(JsonElement root)
    {
        var comments = new List<string>();

        foreach (var comment in root.EnumerateArray())
        {
            if (comment.TryGetProperty("body", out var bodyElement) &&
                bodyElement.ValueKind == JsonValueKind.String)
            {
                var body = bodyElement.GetString();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    comments.Add(body);
                }
            }
        }

        return comments;
    }

    private static List<GitHubIssueDto> ParseIssuesFromJson(JsonElement root)
    {
        var issues = new List<GitHubIssueDto>();

        foreach (var ghIssue in root.EnumerateArray())
        {
            var isPullRequest = ghIssue.TryGetProperty("pull_request", out _);

            var number = ghIssue.GetProperty("number").GetInt32();
            var title = ghIssue.GetProperty("title").GetString() ?? "";
            var body = ghIssue.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString()
                : null;
            var state = ghIssue.GetProperty("state").GetString() ?? "open";
            var htmlUrl = ghIssue.GetProperty("html_url").GetString() ?? "";
            var updatedAt = ghIssue.GetProperty("updated_at").GetDateTimeOffset();

            string? parentIssueUrl = null;
            if (ghIssue.TryGetProperty("parent_issue_url", out var parentUrlElement) &&
                parentUrlElement.ValueKind == JsonValueKind.String)
            {
                parentIssueUrl = parentUrlElement.GetString();
            }

            var labelNames = new List<string>();
            if (ghIssue.TryGetProperty("labels", out var labelsElement))
            {
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
            }

            issues.Add(new GitHubIssueDto(
                number,
                title,
                body,
                state,
                htmlUrl,
                updatedAt,
                parentIssueUrl,
                labelNames,
                isPullRequest));
        }

        return issues;
    }
}
