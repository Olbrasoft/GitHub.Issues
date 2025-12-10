using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// GitHub GraphQL API client for batch-fetching issue bodies.
/// </summary>
public class GitHubGraphQLClient : IGitHubGraphQLClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubGraphQLClient> _logger;

    public GitHubGraphQLClient(
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubGraphQLClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.GraphQLEndpoint);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubIssuesSearch", "1.0"));

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }

    public async Task<Dictionary<(string Owner, string Repo, int Number), string>> FetchBodiesAsync(
        IEnumerable<IssueBodyRequest> issues,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<(string Owner, string Repo, int Number), string>();
        var issueList = issues.ToList();

        if (issueList.Count == 0)
            return result;

        if (string.IsNullOrEmpty(_settings.Token))
        {
            _logger.LogWarning("GitHub token not configured, cannot fetch issue bodies");
            return result;
        }

        try
        {
            var query = BuildQuery(issueList);
            var response = await ExecuteQueryAsync(query, cancellationToken);

            if (response.HasValue)
            {
                ParseResponse(response.Value, issueList, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch issue bodies from GitHub GraphQL API");
        }

        return result;
    }

    private static string BuildQuery(List<IssueBodyRequest> issues)
    {
        // Group issues by repository for efficient querying
        var byRepo = issues.GroupBy(i => (i.Owner, i.Repo));

        var sb = new StringBuilder();
        sb.AppendLine("{");

        var repoIndex = 0;
        foreach (var repoGroup in byRepo)
        {
            var repoAlias = $"repo{repoIndex++}";
            sb.AppendLine($"  {repoAlias}: repository(owner: \"{repoGroup.Key.Owner}\", name: \"{repoGroup.Key.Repo}\") {{");

            foreach (var issue in repoGroup)
            {
                var issueAlias = $"issue{issue.Number}";
                sb.AppendLine($"    {issueAlias}: issue(number: {issue.Number}) {{ body number }}");
            }

            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private async Task<JsonElement?> ExecuteQueryAsync(string query, CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new { query });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("GitHub GraphQL API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(responseBody);

        if (json.RootElement.TryGetProperty("errors", out var errors))
        {
            _logger.LogWarning("GitHub GraphQL API returned errors: {Errors}", errors.ToString());
        }

        if (json.RootElement.TryGetProperty("data", out var data))
        {
            return data;
        }

        return null;
    }

    private void ParseResponse(
        JsonElement data,
        List<IssueBodyRequest> issues,
        Dictionary<(string Owner, string Repo, int Number), string> result)
    {
        // Group issues by repository to match query structure
        var byRepo = issues.GroupBy(i => (i.Owner, i.Repo)).ToList();

        var repoIndex = 0;
        foreach (var repoGroup in byRepo)
        {
            var repoAlias = $"repo{repoIndex++}";

            if (!data.TryGetProperty(repoAlias, out var repoData) || repoData.ValueKind == JsonValueKind.Null)
            {
                _logger.LogWarning("Repository {Owner}/{Repo} not found in GraphQL response",
                    repoGroup.Key.Owner, repoGroup.Key.Repo);
                continue;
            }

            foreach (var issue in repoGroup)
            {
                var issueAlias = $"issue{issue.Number}";

                if (repoData.TryGetProperty(issueAlias, out var issueData) &&
                    issueData.ValueKind != JsonValueKind.Null &&
                    issueData.TryGetProperty("body", out var bodyProp))
                {
                    var body = bodyProp.GetString() ?? string.Empty;
                    result[(repoGroup.Key.Owner, repoGroup.Key.Repo, issue.Number)] = body;
                }
                else
                {
                    _logger.LogDebug("Issue {Owner}/{Repo}#{Number} body not found in response",
                        issue.Owner, issue.Repo, issue.Number);
                }
            }
        }
    }
}
