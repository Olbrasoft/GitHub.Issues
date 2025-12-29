using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Strategies;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;
using static Olbrasoft.GitHub.Issues.Business.Services.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Strategies;

public class RepositoryBrowseStrategyTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<RepositoryBrowseStrategy>> _mockLogger = new();
    private readonly IOptions<AiSummarySettings> _options = Options.Create(new AiSummarySettings { MaxLength = 500 });

    private RepositoryBrowseStrategy CreateStrategy()
    {
        return new RepositoryBrowseStrategy(_mockMediator.Object, _mockLogger.Object, _options);
    }

    [Fact]
    public void Priority_IsLower()
    {
        // Arrange
        var strategy = CreateStrategy();

        // Assert
        Assert.Equal(50, strategy.Priority);
    }

    [Fact]
    public void CanHandle_WhenNoSearchAndHasRepositoryFilter_ReturnsTrue()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            RepositoryIds = [1, 2]
        };

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WhenHasIssueNumbers_ReturnsFalse()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(123, null)],
            RepositoryIds = [1, 2]
        };

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanHandle_WhenHasSemanticQuery_ReturnsFalse()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = "test search",
            RepositoryIds = [1, 2]
        };

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanHandle_WhenNoRepositoryFilter_ReturnsFalse()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria();

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsIssuesFromRepository()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            RepositoryIds = [1],
            State = "open",
            Page = 1,
            PageSize = 10
        };

        var listResults = new IssueSearchPageDto
        {
            Results =
            [
                new IssueSearchResultDto
                {
                    Id = 1,
                    IssueNumber = 42,
                    Title = "Test Issue",
                    IsOpen = true,
                    Url = "url",
                    RepositoryFullName = "test/repo",
                    Labels = []
                }
            ],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalPages = 1
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResults);

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(42, result.Results[0].IssueNumber);
        Assert.True(result.IsTerminal);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTerminalResult()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            RepositoryIds = [1],
            State = "all",
            Page = 2,
            PageSize = 5
        };

        var listResults = new IssueSearchPageDto
        {
            Results = [],
            TotalCount = 10,
            Page = 2,
            PageSize = 5,
            TotalPages = 2
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResults);

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.True(result.IsTerminal);
        Assert.Equal(10, result.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCriteriaToQuery()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            RepositoryIds = [1, 2],
            State = "closed",
            Page = 3,
            PageSize = 15
        };

        _mockMediator.Setup(m => m.MediateAsync(
            It.Is<IssueListQuery>(q =>
                q.State == "closed" &&
                q.Page == 3 &&
                q.PageSize == 15 &&
                q.RepositoryIds != null &&
                q.RepositoryIds.SequenceEqual(new[] { 1, 2 })),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSearchPageDto
            {
                Results = [],
                TotalCount = 0,
                Page = 3,
                PageSize = 15,
                TotalPages = 0
            });

        // Act
        await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert - Verify the setup was called (implicit validation via It.Is<>)
        _mockMediator.Verify(m => m.MediateAsync(
            It.Is<IssueListQuery>(q => q.State == "closed" && q.Page == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
