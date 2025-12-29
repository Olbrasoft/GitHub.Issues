using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.Text.Transformation.Abstractions;
using Xunit;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Summarization;

public class AiSummarizationServiceTests
{
    private readonly Mock<ISummarizationService> _mockSummarizationService;
    private readonly Mock<ILogger<AiSummarizationService>> _mockLogger;
    private readonly AiSummarizationService _service;

    public AiSummarizationServiceTests()
    {
        _mockSummarizationService = new Mock<ISummarizationService>();
        _mockLogger = new Mock<ILogger<AiSummarizationService>>();

        _service = new AiSummarizationService(
            _mockSummarizationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        const string body = "Test issue body";
        const string expectedSummary = "Test summary";
        const string provider = "Cerebras";
        const string model = "llama3.1-8b";

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = expectedSummary,
                Provider = provider,
                Model = model
            });

        // Act
        var result = await _service.GenerateSummaryAsync(body);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedSummary, result.Summary);
        Assert.Equal($"{provider}/{model}", result.Provider);
        Assert.Null(result.Error);

        _mockSummarizationService.Verify(s => s.SummarizeAsync(
            body,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenFails_ReturnsFailureResult()
    {
        // Arrange
        const string body = "Test issue body";
        const string errorMessage = "API rate limit exceeded";
        const string provider = "Cerebras";

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = false,
                Summary = null,
                Provider = provider,
                Error = errorMessage
            });

        // Act
        var result = await _service.GenerateSummaryAsync(body);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Summary);
        Assert.Equal(provider, result.Provider);
        Assert.Equal(errorMessage, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateSummaryAsync_WhenBodyEmpty_ReturnsFailureResult(string? body)
    {
        // Act
        var result = await _service.GenerateSummaryAsync(body!);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Summary);
        Assert.Null(result.Provider);
        Assert.Equal("Body is empty or whitespace", result.Error);

        _mockSummarizationService.Verify(s => s.SummarizeAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenSummaryEmpty_ReturnsFailureResult()
    {
        // Arrange
        const string body = "Test body";

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = "",  // Empty summary
                Provider = "TestProvider"
            });

        // Act
        var result = await _service.GenerateSummaryAsync(body);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Summary);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenException_ReturnsFailureResult()
    {
        // Arrange
        const string body = "Test body";
        const string exceptionMessage = "Network error";

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(exceptionMessage));

        // Act
        var result = await _service.GenerateSummaryAsync(body);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Summary);
        Assert.Null(result.Provider);
        Assert.Contains(exceptionMessage, result.Error);
    }

    [Fact]
    public async Task GenerateSummaryAsync_PassesCancellationToken()
    {
        // Arrange
        const string body = "Test body";
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, token))
            .ReturnsAsync(new SummarizationResult
            {
                Success = true,
                Summary = "Summary",
                Provider = "Test"
            });

        // Act
        await _service.GenerateSummaryAsync(body, token);

        // Assert
        _mockSummarizationService.Verify(s => s.SummarizeAsync(
            body,
            token), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenNoErrorMessage_UsesDefaultError()
    {
        // Arrange
        const string body = "Test body";

        _mockSummarizationService
            .Setup(s => s.SummarizeAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarizationResult
            {
                Success = false,
                Summary = null,
                Provider = "Test",
                Error = null  // No error message
            });

        // Act
        var result = await _service.GenerateSummaryAsync(body);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Summarization failed with no error message", result.Error);
    }
}
