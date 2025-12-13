using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.ApiClients;

public class GitHubEventApiClientTests
{
    private readonly Mock<ILogger<GitHubEventApiClient>> _loggerMock;
    private readonly IOptions<SyncSettings> _settings;

    public GitHubEventApiClientTests()
    {
        _loggerMock = new Mock<ILogger<GitHubEventApiClient>>();
        _settings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
    }

    [Fact]
    public async Task FetchEventsAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("[]");
        var client = new GitHubEventApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchEventsAsync("owner", "repo");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchEventsAsync_WithSingleEvent_ReturnsParsedEvent()
    {
        // Arrange
        var json = """
        [
            {
                "id": 12345,
                "event": "labeled",
                "created_at": "2024-01-15T10:00:00Z",
                "actor": {
                    "id": 100,
                    "login": "testuser"
                },
                "issue": {
                    "number": 42
                }
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubEventApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchEventsAsync("owner", "repo");

        // Assert
        Assert.Single(result);
        var evt = result[0];
        Assert.Equal(12345, evt.GitHubEventId);
        Assert.Equal(42, evt.IssueNumber);
        Assert.Equal("labeled", evt.EventType);
    }

    [Fact]
    public async Task FetchEventsAsync_WithoutIssue_SkipsEvent()
    {
        // Arrange
        var json = """
        [
            {
                "id": 12345,
                "event": "labeled",
                "created_at": "2024-01-15T10:00:00Z"
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubEventApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchEventsAsync("owner", "repo");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchEventsAsync_WithSince_StopsAtOlderEvents()
    {
        // Arrange - Events returned in descending order (newest first)
        var json = """
        [
            {
                "id": 200,
                "event": "closed",
                "created_at": "2024-01-20T10:00:00Z",
                "issue": { "number": 2 }
            },
            {
                "id": 100,
                "event": "opened",
                "created_at": "2024-01-10T10:00:00Z",
                "issue": { "number": 1 }
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubEventApiClient(httpClient, _settings, _loggerMock.Object);
        var since = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await client.FetchEventsAsync("owner", "repo", since);

        // Assert - Only the event from Jan 20 should be returned
        Assert.Single(result);
        Assert.Equal(200, result[0].GitHubEventId);
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
