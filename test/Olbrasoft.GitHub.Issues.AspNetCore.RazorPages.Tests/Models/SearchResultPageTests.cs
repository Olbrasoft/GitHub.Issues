using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests.Models;

public class SearchResultPageTests
{
    [Fact]
    public void HasPreviousPage_WhenPageIsOne_ReturnsFalse()
    {
        // Arrange
        var page = new SearchResultPage { Page = 1 };

        // Act & Assert
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public void HasPreviousPage_WhenPageIsGreaterThanOne_ReturnsTrue()
    {
        // Arrange
        var page = new SearchResultPage { Page = 2 };

        // Act & Assert
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public void HasNextPage_WhenPageEqualsTotal_ReturnsFalse()
    {
        // Arrange
        var page = new SearchResultPage { Page = 5, TotalPages = 5 };

        // Act & Assert
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public void HasNextPage_WhenPageLessThanTotal_ReturnsTrue()
    {
        // Arrange
        var page = new SearchResultPage { Page = 3, TotalPages = 5 };

        // Act & Assert
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void Results_DefaultsToEmptyList()
    {
        // Arrange & Act
        var page = new SearchResultPage();

        // Assert
        Assert.NotNull(page.Results);
        Assert.Empty(page.Results);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var page = new SearchResultPage();

        // Assert
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }
}
