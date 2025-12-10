using Moq;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Data.Tests.Queries;

public class RepositoryQueriesTests
{
    [Fact]
    public void RepositoriesSearchQuery_HasCorrectDefaults()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();

        // Act
        var query = new RepositoriesSearchQuery(mediatorMock.Object);

        // Assert
        Assert.Equal(string.Empty, query.Term);
        Assert.Equal(15, query.MaxResults);
    }

    [Fact]
    public void RepositoriesSearchQuery_CanSetProperties()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();
        var query = new RepositoriesSearchQuery(mediatorMock.Object);

        // Act
        query.Term = "Virtual";
        query.MaxResults = 10;

        // Assert
        Assert.Equal("Virtual", query.Term);
        Assert.Equal(10, query.MaxResults);
    }

    [Fact]
    public void RepositoriesByIdsQuery_HasCorrectDefaults()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();

        // Act
        var query = new RepositoriesByIdsQuery(mediatorMock.Object);

        // Assert
        Assert.Empty(query.Ids);
    }

    [Fact]
    public void RepositoriesByIdsQuery_CanSetIds()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();
        var query = new RepositoriesByIdsQuery(mediatorMock.Object);
        var ids = new List<int> { 1, 2, 3 };

        // Act
        query.Ids = ids;

        // Assert
        Assert.Equal(3, query.Ids.Count);
        Assert.Contains(1, query.Ids);
        Assert.Contains(2, query.Ids);
        Assert.Contains(3, query.Ids);
    }
}
