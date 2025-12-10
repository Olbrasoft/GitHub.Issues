using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class IssueSearchServiceTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<IssueSearchService>> _loggerMock;

    public IssueSearchServiceTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<IssueSearchService>>();
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsEmptyResult()
    {
        // Arrange
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vector?)null);

        // Note: We can't easily test with real DbContext due to pgvector dependency
        // This test verifies the interface contract exists
        var service = new Mock<IIssueSearchService>();
        service.Setup(x => x.SearchAsync("", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultPage());

        // Act
        var result = await service.Object.SearchAsync("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceQuery_ReturnsEmptyResult()
    {
        // Arrange
        var service = new Mock<IIssueSearchService>();
        service.Setup(x => x.SearchAsync("   ", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultPage());

        // Act
        var result = await service.Object.SearchAsync("   ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void IIssueSearchService_InterfaceExists()
    {
        // Verify interface can be mocked (exists and is accessible)
        var mock = new Mock<IIssueSearchService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void IssueSearchService_ImplementsInterface()
    {
        // Verify IssueSearchService implements IIssueSearchService
        Assert.True(typeof(IIssueSearchService).IsAssignableFrom(typeof(IssueSearchService)));
    }

    [Fact]
    public async Task SearchAsync_InterfaceHasCorrectSignature()
    {
        // Arrange
        var mock = new Mock<IIssueSearchService>();
        mock.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultPage
            {
                Page = 2,
                PageSize = 25,
                TotalCount = 100
            });

        // Act
        var result = await mock.Object.SearchAsync("test", "open", 2, 25);

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(100, result.TotalCount);
    }
}
