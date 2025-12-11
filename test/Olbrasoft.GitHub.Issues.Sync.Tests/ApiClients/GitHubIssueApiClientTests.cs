using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.ApiClients;

public class GitHubIssueApiClientTests
{
    private readonly Mock<ILogger<GitHubIssueApiClient>> _loggerMock;
    private readonly IOptions<SyncSettings> _settings;

    public GitHubIssueApiClientTests()
    {
        _loggerMock = new Mock<ILogger<GitHubIssueApiClient>>();
        _settings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
    }

    [Fact]
    public async Task FetchIssuesAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("[]");
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchIssuesAsync_WithSingleIssue_ReturnsParsedIssue()
    {
        // Arrange
        var json = """
        [
            {
                "number": 1,
                "title": "Test Issue",
                "body": "Test body",
                "state": "open",
                "html_url": "https://github.com/owner/repo/issues/1",
                "updated_at": "2024-01-15T10:00:00Z",
                "labels": [{"name": "bug"}]
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        var issue = result[0];
        Assert.Equal(1, issue.Number);
        Assert.Equal("Test Issue", issue.Title);
        Assert.Equal("Test body", issue.Body);
        Assert.Equal("open", issue.State);
        Assert.False(issue.IsPullRequest);
        Assert.Single(issue.LabelNames);
        Assert.Equal("bug", issue.LabelNames[0]);
    }

    [Fact]
    public async Task FetchIssuesAsync_WithPullRequest_MarksAsPullRequest()
    {
        // Arrange
        var json = """
        [
            {
                "number": 1,
                "title": "PR Title",
                "state": "open",
                "html_url": "https://github.com/owner/repo/pull/1",
                "updated_at": "2024-01-15T10:00:00Z",
                "pull_request": {"url": "..."},
                "labels": []
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsPullRequest);
    }

    [Fact]
    public async Task FetchIssuesAsync_WithParentIssueUrl_ExtractsParentUrl()
    {
        // Arrange
        var json = """
        [
            {
                "number": 2,
                "title": "Child Issue",
                "state": "open",
                "html_url": "https://github.com/owner/repo/issues/2",
                "updated_at": "2024-01-15T10:00:00Z",
                "parent_issue_url": "https://api.github.com/repos/owner/repo/issues/1",
                "labels": []
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues/1", result[0].ParentIssueUrl);
    }

    [Fact]
    public async Task FetchIssuesAsync_WithNullBody_HandlesGracefully()
    {
        // Arrange
        var json = """
        [
            {
                "number": 1,
                "title": "Issue without body",
                "body": null,
                "state": "open",
                "html_url": "https://github.com/owner/repo/issues/1",
                "updated_at": "2024-01-15T10:00:00Z",
                "labels": []
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        Assert.Null(result[0].Body);
    }

    [Fact]
    public async Task FetchIssuesAsync_WithMultipleLabels_ExtractsAllLabels()
    {
        // Arrange
        var json = """
        [
            {
                "number": 1,
                "title": "Multi-label Issue",
                "state": "open",
                "html_url": "https://github.com/owner/repo/issues/1",
                "updated_at": "2024-01-15T10:00:00Z",
                "labels": [{"name": "bug"}, {"name": "enhancement"}, {"name": "help wanted"}]
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubIssueApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchIssuesAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        Assert.Equal(3, result[0].LabelNames.Count);
        Assert.Contains("bug", result[0].LabelNames);
        Assert.Contains("enhancement", result[0].LabelNames);
        Assert.Contains("help wanted", result[0].LabelNames);
    }

    private static HttpClient CreateMockHttpClient(string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
    }
}
