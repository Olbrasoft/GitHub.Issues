using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business.GraphQL;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// GitHub GraphQL API client for batch-fetching issue bodies.
/// REFACTORED (Issue #279): Now uses IGraphQLQueryBuilder and IGraphQLResponseParser for SRP.
/// </summary>
public class GitHubGraphQLClient : IGitHubGraphQLClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly IGraphQLQueryBuilder _queryBuilder;
    private readonly IGraphQLResponseParser _responseParser;
    private readonly ILogger<GitHubGraphQLClient> _logger;

    public GitHubGraphQLClient(
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        IGraphQLQueryBuilder queryBuilder,
        IGraphQLResponseParser responseParser,
        ILogger<GitHubGraphQLClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(queryBuilder);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _settings = settings.Value;
        _queryBuilder = queryBuilder;
        _responseParser = responseParser;
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
            // Delegate to GraphQLQueryBuilder for query construction
            var query = _queryBuilder.BuildBatchIssueBodyQuery(issueList);
            var response = await ExecuteQueryAsync(query, cancellationToken);

            if (response.HasValue)
            {
                // Delegate to GraphQLResponseParser for parsing
                result = _responseParser.ParseBatchIssueBodyResponse(response.Value, issueList);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch issue bodies from GitHub GraphQL API");
        }

        return result;
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

}
