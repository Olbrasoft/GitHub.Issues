using Olbrasoft.GitHub.Issues.Business.Strategies;
using static Olbrasoft.GitHub.Issues.Business.Services.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Strategies;

public class SearchCriteriaTests
{
    [Fact]
    public void HasIssueNumbers_WhenParsedNumbersNotEmpty_ReturnsTrue()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            ParsedIssueNumbers = [new ParsedIssueNumber(123, null)]
        };

        // Assert
        Assert.True(criteria.HasIssueNumbers);
    }

    [Fact]
    public void HasIssueNumbers_WhenParsedNumbersEmpty_ReturnsFalse()
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Assert
        Assert.False(criteria.HasIssueNumbers);
    }

    [Fact]
    public void HasSemanticQuery_WhenSemanticQueryNotEmpty_ReturnsTrue()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            SemanticQuery = "test query"
        };

        // Assert
        Assert.True(criteria.HasSemanticQuery);
    }

    [Fact]
    public void HasSemanticQuery_WhenSemanticQueryEmpty_ReturnsFalse()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            SemanticQuery = ""
        };

        // Assert
        Assert.False(criteria.HasSemanticQuery);
    }

    [Fact]
    public void HasSemanticQuery_WhenSemanticQueryNull_ReturnsFalse()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            SemanticQuery = null
        };

        // Assert
        Assert.False(criteria.HasSemanticQuery);
    }

    [Fact]
    public void HasRepositoryFilter_WhenRepositoryIdsNotEmpty_ReturnsTrue()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            RepositoryIds = [1, 2]
        };

        // Assert
        Assert.True(criteria.HasRepositoryFilter);
    }

    [Fact]
    public void HasRepositoryFilter_WhenRepositoryIdsEmpty_ReturnsFalse()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            RepositoryIds = []
        };

        // Assert
        Assert.False(criteria.HasRepositoryFilter);
    }

    [Fact]
    public void HasRepositoryFilter_WhenRepositoryIdsNull_ReturnsFalse()
    {
        // Arrange
        var criteria = new SearchCriteria
        {
            RepositoryIds = null
        };

        // Assert
        Assert.False(criteria.HasRepositoryFilter);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var criteria = new SearchCriteria();

        // Assert
        Assert.Equal(string.Empty, criteria.Query);
        Assert.Empty(criteria.ParsedIssueNumbers);
        Assert.Null(criteria.SemanticQuery);
        Assert.Equal("all", criteria.State);
        Assert.Equal(1, criteria.Page);
        Assert.Equal(10, criteria.PageSize);
        Assert.Null(criteria.RepositoryIds);
    }
}
