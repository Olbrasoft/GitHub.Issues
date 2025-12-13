using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class RoundRobinTranslatorTests
{
    private readonly Mock<ILogger<RoundRobinTranslator>> _mockLogger = new();

    [Fact]
    public void Constructor_WithEmptyTranslators_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RoundRobinTranslator([], _mockLogger.Object));
    }

    [Fact]
    public void TranslatorCount_ReturnsCorrectCount()
    {
        // Arrange
        var translators = CreateMockTranslators(3);
        var roundRobin = new RoundRobinTranslator(translators, _mockLogger.Object);

        // Assert
        Assert.Equal(3, roundRobin.TranslatorCount);
    }

    [Fact]
    public async Task TranslateAsync_RotatesThroughTranslators()
    {
        // Arrange
        var translator1 = CreateSuccessfulTranslator("Translation1", "Provider1");
        var translator2 = CreateSuccessfulTranslator("Translation2", "Provider2");
        var translator3 = CreateSuccessfulTranslator("Translation3", "Provider3");

        var roundRobin = new RoundRobinTranslator(
            [translator1.Object, translator2.Object, translator3.Object],
            _mockLogger.Object);

        // Act - Call 6 times to see full rotation twice
        var results = new List<TranslatorResult>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await roundRobin.TranslateAsync("text", "cs"));
        }

        // Assert - Each translator should be called exactly twice
        translator1.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2));
        translator2.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2));
        translator3.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2));
    }

    [Fact]
    public async Task TranslateAsync_WhenFirstFails_FallsBackToNext()
    {
        // Arrange
        var failingTranslator = CreateFailingTranslator("Error1", "Provider1");
        var successfulTranslator = CreateSuccessfulTranslator("Success", "Provider2");

        var roundRobin = new RoundRobinTranslator(
            [failingTranslator.Object, successfulTranslator.Object],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Translation);
        Assert.Equal("Provider2", result.Provider);
    }

    [Fact]
    public async Task TranslateAsync_WhenAllFail_ReturnsFailure()
    {
        // Arrange
        var translator1 = CreateFailingTranslator("Error1", "Provider1");
        var translator2 = CreateFailingTranslator("Error2", "Provider2");

        var roundRobin = new RoundRobinTranslator(
            [translator1.Object, translator2.Object],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("All 2 translators failed", result.Error);
    }

    [Fact]
    public async Task TranslateAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        var translator = CreateSuccessfulTranslator("Translation", "Provider");
        var roundRobin = new RoundRobinTranslator([translator.Object], _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("", "cs");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Empty text", result.Error);
        translator.Verify(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateAsync_WhenTranslatorThrows_FallsBackToNext()
    {
        // Arrange
        var throwingTranslator = new Mock<ITranslator>();
        throwingTranslator
            .Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var successfulTranslator = CreateSuccessfulTranslator("Success", "Provider2");

        var roundRobin = new RoundRobinTranslator(
            [throwingTranslator.Object, successfulTranslator.Object],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Translation);
    }

    [Fact]
    public async Task TranslateAsync_StartsFromNextTranslatorOnSubsequentCalls()
    {
        // Arrange
        var translator1 = CreateSuccessfulTranslator("T1", "P1");
        var translator2 = CreateSuccessfulTranslator("T2", "P2");

        var roundRobin = new RoundRobinTranslator(
            [translator1.Object, translator2.Object],
            _mockLogger.Object);

        // Act - First call starts at index 1 (after increment from 0)
        var result1 = await roundRobin.TranslateAsync("text1", "cs");
        var result2 = await roundRobin.TranslateAsync("text2", "cs");

        // Assert - Results alternate between translators
        Assert.NotEqual(result1.Provider, result2.Provider);
    }

    private static Mock<ITranslator> CreateSuccessfulTranslator(string translation, string provider)
    {
        var mock = new Mock<ITranslator>();
        mock.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Ok(translation, provider, "en"));
        return mock;
    }

    private static Mock<ITranslator> CreateFailingTranslator(string error, string provider)
    {
        var mock = new Mock<ITranslator>();
        mock.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TranslatorResult.Fail(error, provider));
        return mock;
    }

    private static ITranslator[] CreateMockTranslators(int count)
    {
        var translators = new ITranslator[count];
        for (int i = 0; i < count; i++)
        {
            var mock = new Mock<ITranslator>();
            mock.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TranslatorResult.Ok($"Translation{i}", $"Provider{i}", "en"));
            translators[i] = mock.Object;
        }
        return translators;
    }
}
