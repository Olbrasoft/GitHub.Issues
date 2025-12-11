using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.ApiClients;

public class GitHubRepositoryApiClientTests
{
    private readonly Mock<ILogger<GitHubRepositoryApiClient>> _loggerMock;
    private readonly IOptions<SyncSettings> _settings;

    public GitHubRepositoryApiClientTests()
    {
        _loggerMock = new Mock<ILogger<GitHubRepositoryApiClient>>();
        _settings = Options.Create(new SyncSettings { GitHubApiPageSize = 100 });
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("[]");
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", true, true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithSingleRepo_ReturnsRepoName()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/repo1",
                "has_issues": true,
                "archived": false,
                "fork": false
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", true, true);

        // Assert
        Assert.Single(result);
        Assert.Equal("owner/repo1", result[0]);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithIssuesDisabled_SkipsRepo()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/repo1",
                "has_issues": false,
                "archived": false,
                "fork": false
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", true, true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithArchivedRepo_SkipsWhenNotIncluded()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/archived-repo",
                "has_issues": true,
                "archived": true,
                "fork": false
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", includeArchived: false, includeForks: true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithArchivedRepo_IncludesWhenFlagSet()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/archived-repo",
                "has_issues": true,
                "archived": true,
                "fork": false
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", includeArchived: true, includeForks: true);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithFork_SkipsWhenNotIncluded()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/forked-repo",
                "has_issues": true,
                "archived": false,
                "fork": true
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", includeArchived: true, includeForks: false);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchRepositoriesForOwnerAsync_WithMultipleRepos_ReturnsFiltered()
    {
        // Arrange
        var json = """
        [
            {
                "full_name": "owner/repo1",
                "has_issues": true,
                "archived": false,
                "fork": false
            },
            {
                "full_name": "owner/archived",
                "has_issues": true,
                "archived": true,
                "fork": false
            },
            {
                "full_name": "owner/repo2",
                "has_issues": true,
                "archived": false,
                "fork": false
            }
        ]
        """;
        var httpClient = CreateMockHttpClient(json);
        var client = new GitHubRepositoryApiClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.FetchRepositoriesForOwnerAsync("owner", "user", includeArchived: false, includeForks: false);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("owner/repo1", result);
        Assert.Contains("owner/repo2", result);
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
