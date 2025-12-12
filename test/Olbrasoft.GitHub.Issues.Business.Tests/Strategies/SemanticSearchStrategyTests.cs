using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Strategies;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Strategies;

public class SemanticSearchStrategyTests
{
    private readonly Mock<IMediator> _mockMediator = new();
    private readonly Mock<IEmbeddingService> _mockEmbeddingService = new();
    private readonly Mock<ILogger<SemanticSearchStrategy>> _mockLogger = new();
    private readonly IOptions<AiSummarySettings> _options = Options.Create(new AiSummarySettings { MaxLength = 500 });

    private SemanticSearchStrategy CreateStrategy()
    {
        return new SemanticSearchStrategy(
            _mockMediator.Object,
            _mockEmbeddingService.Object,
            _mockLogger.Object,
            _options);
    }

    [Fact]
    public void Priority_IsMediumHigh()
    {
        // Arrange
        var strategy = CreateStrategy();

        // Assert
        Assert.Equal(80, strategy.Priority);
    }

    [Fact]
    public void CanHandle_WhenHasSemanticQuery_ReturnsTrue()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = "test search"
        };

        // Act
        var result = strategy.CanHandle(criteria);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WhenNoSemanticQuery_ReturnsFalse()
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
    public async Task ExecuteAsync_UsesVectorSearch_WhenEmbeddingAvailable()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = "fix bug",
            PageSize = 10,
            State = "all"
        };

        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService.Setup(e => e.GenerateEmbeddingAsync("fix bug", EmbeddingInputType.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        var searchResults = new IssueSearchPageDto
        {
            Results =
            [
                new IssueSearchResultDto
                {
                    Id = 1,
                    IssueNumber = 42,
                    Title = "Bug Fix",
                    IsOpen = true,
                    Url = "url",
                    RepositoryFullName = "test/repo",
                    Similarity = 0.9,
                    Labels = []
                }
            ],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalPages = 1
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(42, result.Results[0].IssueNumber);
        Assert.False(result.Results[0].IsExactMatch);
        _mockMediator.Verify(m => m.MediateAsync(It.IsAny<IssueSearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToTextSearch_WhenEmbeddingUnavailable()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = "fix bug",
            PageSize = 10,
            State = "all"
        };

        _mockEmbeddingService.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingInputType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);

        var textSearchResults = new IssueSearchPageDto
        {
            Results =
            [
                new IssueSearchResultDto
                {
                    Id = 1,
                    IssueNumber = 42,
                    Title = "Bug Fix",
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

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueTextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(textSearchResults);

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        _mockMediator.Verify(m => m.MediateAsync(It.IsAny<IssueTextSearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.MediateAsync(It.IsAny<IssueSearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyResult_WhenSemanticQueryEmpty()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = ""
        };

        // Act
        var result = await strategy.ExecuteAsync(criteria, new HashSet<int>(), CancellationToken.None);

        // Assert
        Assert.Empty(result.Results);
        _mockEmbeddingService.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingInputType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesExistingResults()
    {
        // Arrange
        var strategy = CreateStrategy();
        var criteria = new SearchCriteria
        {
            SemanticQuery = "test",
            PageSize = 10,
            State = "all"
        };

        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingInputType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        var searchResults = new IssueSearchPageDto
        {
            Results =
            [
                new IssueSearchResultDto { Id = 1, IssueNumber = 1, Title = "Issue 1", IsOpen = true, Url = "url", RepositoryFullName = "test/repo", Labels = [] },
                new IssueSearchResultDto { Id = 2, IssueNumber = 2, Title = "Issue 2", IsOpen = true, Url = "url", RepositoryFullName = "test/repo", Labels = [] }
            ],
            TotalCount = 2,
            Page = 1,
            PageSize = 10,
            TotalPages = 1
        };

        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<IssueSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var existingIds = new HashSet<int> { 1 };

        // Act
        var result = await strategy.ExecuteAsync(criteria, existingIds, CancellationToken.None);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal(2, result.Results[0].Id);
    }
}
