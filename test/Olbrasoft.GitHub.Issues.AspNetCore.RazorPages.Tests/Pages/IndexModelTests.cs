using Moq;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests.Pages;

public class IndexModelTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange
        var searchServiceMock = new Mock<IIssueSearchService>();
        var mediatorMock = new Mock<IMediator>();
        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });

        // Act
        var model = new IndexModel(searchServiceMock.Object, searchSettings, mediatorMock.Object);

        // Assert
        Assert.Null(model.Query);
        Assert.Equal("open", model.State); // Default changed to "open" in Issue #105
        Assert.Equal(1, model.PageNumber);
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyQuery_DoesNotSearch()
    {
        // Arrange
        var searchServiceMock = new Mock<IIssueSearchService>();
        var mediatorMock = new Mock<IMediator>();
        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });
        var model = new IndexModel(searchServiceMock.Object, searchSettings, mediatorMock.Object);

        // Act
        await model.OnGetAsync(CancellationToken.None);

        // Assert
        searchServiceMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnGetAsync_WithQuery_SearchesAndSetsResult()
    {
        // Arrange
        var expectedResult = new SearchResultPage { TotalCount = 5 };
        var searchServiceMock = new Mock<IIssueSearchService>();
        var mediatorMock = new Mock<IMediator>();
        searchServiceMock
            .Setup(x => x.SearchAsync("test", "open", 1, 10, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var searchSettings = Options.Create(new SearchSettings { DefaultPageSize = 10, PageSizeOptions = [10, 25, 50] });
        var model = new IndexModel(searchServiceMock.Object, searchSettings, mediatorMock.Object)
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
