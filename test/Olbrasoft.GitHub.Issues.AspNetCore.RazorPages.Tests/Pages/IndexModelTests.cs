using Moq;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests.Pages;

public class IndexModelTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange
        var searchServiceMock = new Mock<IIssueSearchService>();
        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });

        // Act
        var model = new IndexModel(searchServiceMock.Object, searchSettings);

        // Assert
        Assert.Null(model.Query);
        Assert.Equal("all", model.State);
        Assert.Equal(1, model.PageNumber);
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyQuery_DoesNotSearch()
    {
        // Arrange
        var searchServiceMock = new Mock<IIssueSearchService>();
        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });
        var model = new IndexModel(searchServiceMock.Object, searchSettings);

        // Act
        await model.OnGetAsync(CancellationToken.None);

        // Assert
        searchServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnGetAsync_WithQuery_SearchesAndSetsResult()
    {
        // Arrange
        var expectedResult = new SearchResultPage { TotalCount = 5 };
        var searchServiceMock = new Mock<IIssueSearchService>();
        searchServiceMock
            .Setup(x => x.SearchAsync("test", "all", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });
        var model = new IndexModel(searchServiceMock.Object, searchSettings)
        {
            Query = "test"
        };

        // Act
        await model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, model.SearchResult);
        Assert.Equal(5, model.SearchResult.TotalCount);
    }
}
