using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Search.Strategies;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;
using static Olbrasoft.GitHub.Issues.Business.Detail.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Strategies;

public class ExactMatchSearchStrategyTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<ILogger<ExactMatchSearchStrategy>> _mockLogger = new();
    private readonly IOptions<AiSummarySettings> _options = Options.Create(new AiSummarySettings { MaxLength = 500 });

    private ExactMatchSearchStrategy CreateStrategy()
    {
        return new ExactMatchSearchStrategy(_mockMediator.Object, _mockLogger.Object, _options);
    }

    [Fact]
    public void Priority_IsHighest()
    {
        // Arrange
        var strategy = CreateStrategy();

        // Assert
        Assert.Equal(100, strategy.Priority);
    }

    [Fact]
    public void CanHandle_WhenHasIssueNumbers_ReturnsTrue()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(123, null)]
        };

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WhenNoIssueNumbers_ReturnsFalse()
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
    public async Task ExecuteAsync_ReturnsMatchingIssues()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(42, null)],
            State = "all"
        };

        var matches = new List<IssueSearchResultDto>
        {
            new()
            {
                Id = 1,
                IssueNumber = 42,
                Title = "Test Issue #42",
                IsOpen = true,
                Url = "url",
                RepositoryFullName = "test/repo",
                Labels = []
            }
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssuesByNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(42, result.Results[0].IssueNumber);
        Assert.True(result.Results[0].IsExactMatch);
        Assert.Contains(1, result.FoundIds);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesExistingResults()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(42, null)],
            State = "all"
        };

        var matches = new List<IssueSearchResultDto>
        {
            new()
            {
                Id = 1,
                IssueNumber = 42,
                Title = "Test Issue #42",
                IsOpen = true,
                Url = "url",
                RepositoryFullName = "test/repo",
                Labels = []
            }
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssuesByNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var existingIds = new HashSet<int> { 1 };

        // Act
        var result = await strategy.ExecuteAsync(criteria, existingIds, CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
        Assert.Empty(result.FoundIds);
    }

    [Fact]
    public async Task ExecuteAsync_PassesRepositoryFilterToQuery()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(42, "test/repo")],
            State = "open",
            RepositoryIds = [1, 2]
        };

        _mockMediator.Setup(m => m.MediateAsync(
            It.Is<IssuesByNumbersQuery>(q =>
                q.RepositoryName == "test/repo" &&
                q.State == "open" &&
                q.RepositoryIds != null &&
                q.RepositoryIds.SequenceEqual(new[] { 1, 2 }) &&
                q.IssueNumbers.Contains(42)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IssueSearchResultDto>());

        // Act
        await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert - Verify the setup was called (implicit validation via It.Is<>)
        _mockMediator.Verify(m => m.MediateAsync(
            It.Is<IssuesByNumbersQuery>(q => q.RepositoryName == "test/repo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
