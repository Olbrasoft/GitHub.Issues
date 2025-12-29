using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class RoundRobinTranslatorTests
{
    private readonly Mock<ILogger<RoundRobinTranslator>> _mockLogger = new();

    [Fact]
    public void Constructor_WithEmptyProviderGroups_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RoundRobinTranslator(Array.Empty<ProviderGroup>(), _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithEmptyTranslators_ThrowsException()
    {
        // Arrange & Act & Assert (legacy constructor)
        Assert.Throws<ArgumentException>(() =>
            new RoundRobinTranslator(Array.Empty<ITranslator>(), _mockLogger.Object));
    }

    [Fact]
    public void TranslatorCount_ReturnsCorrectCount()
    {
        // Arrange - 1 Azure key, 2 DeepL keys
        var azureGroup = CreateProviderGroup("Azure", 1);
        var deepLGroup = CreateProviderGroup("DeepL", 2);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Assert
        Assert.Equal(3, roundRobin.TranslatorCount);
        Assert.Equal(2, roundRobin.ProviderCount);
    }

    [Fact]
    public async Task TranslateAsync_StrictlyAlternatesBetweenProviders()
    {
        // Arrange - 1 Azure key, 2 DeepL keys
        var azure1 = CreateSuccessfulTranslator("Azure-T1", "Azure");
        var deepL1 = CreateSuccessfulTranslator("DeepL-T1", "DeepL");
        var deepL2 = CreateSuccessfulTranslator("DeepL-T2", "DeepL");

        var azureGroup = new ProviderGroup("Azure", [azure1.Object]);
        var deepLGroup = new ProviderGroup("DeepL", [deepL1.Object, deepL2.Object]);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Act - 6 calls to see full pattern
        var results = new List<TranslatorResult>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await roundRobin.TranslateAsync("text", "cs"));
        }

        // Assert - Pattern should be: Azure, DeepL, Azure, DeepL, Azure, DeepL
        // Azure: 3 calls (always key 0)
        // DeepL: 3 calls alternating keys (0, 1, 0)
        Assert.Equal("Azure", results[0].Provider);
        Assert.Equal("DeepL", results[1].Provider);
        Assert.Equal("Azure", results[2].Provider);
        Assert.Equal("DeepL", results[3].Provider);
        Assert.Equal("Azure", results[4].Provider);
        Assert.Equal("DeepL", results[5].Provider);

        // Azure called 3 times
        azure1.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(3));

        // DeepL keys rotated
        deepL1.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2)); // calls 1, 5 (0-indexed: 0, 4 -> positions 1, 5)
        deepL2.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(1)); // call 3 (0-indexed: 2 -> position 3)
    }

    [Fact]
    public async Task TranslateAsync_WhenFirstProviderFails_FallsBackToSecondProvider()
    {
        // Arrange
        var failingAzure = CreateFailingTranslator("Azure error", "Azure");
        var successfulDeepL = CreateSuccessfulTranslator("DeepL translation", "DeepL");

        var azureGroup = new ProviderGroup("Azure", [failingAzure.Object]);
        var deepLGroup = new ProviderGroup("DeepL", [successfulDeepL.Object]);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Act - First request starts with Azure
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("DeepL translation", result.Translation);
        Assert.Equal("DeepL", result.Provider);
    }

    [Fact]
    public async Task TranslateAsync_WhenDeepLKey1Fails_FallsBackToDeepLKey2()
    {
        // Arrange - Azure fails, DeepL key 1 fails, DeepL key 2 succeeds
        var failingAzure = CreateFailingTranslator("Azure error", "Azure");
        var failingDeepL1 = CreateFailingTranslator("DeepL1 error", "DeepL-1");
        var successfulDeepL2 = CreateSuccessfulTranslator("DeepL2 translation", "DeepL-2");

        var azureGroup = new ProviderGroup("Azure", [failingAzure.Object]);
        var deepLGroup = new ProviderGroup("DeepL", [failingDeepL1.Object, successfulDeepL2.Object]);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert - Should fall back through all translators
        Assert.True(result.Success);
        Assert.Equal("DeepL2 translation", result.Translation);
        Assert.Equal("DeepL-2", result.Provider);
    }

    [Fact]
    public async Task TranslateAsync_WhenAllFail_ReturnsFailure()
    {
        // Arrange
        var failingAzure = CreateFailingTranslator("Azure error", "Azure");
        var failingDeepL1 = CreateFailingTranslator("DeepL1 error", "DeepL-1");
        var failingDeepL2 = CreateFailingTranslator("DeepL2 error", "DeepL-2");

        var azureGroup = new ProviderGroup("Azure", [failingAzure.Object]);
        var deepLGroup = new ProviderGroup("DeepL", [failingDeepL1.Object, failingDeepL2.Object]);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("All 3 translators failed", result.Error);
    }

    [Fact]
    public async Task TranslateAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        var translator = CreateSuccessfulTranslator("Translation", "Provider");
        var group = new ProviderGroup("Test", [translator.Object]);
        var roundRobin = new RoundRobinTranslator([group], _mockLogger.Object);

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

        var successfulTranslator = CreateSuccessfulTranslator("Success", "DeepL");

        var azureGroup = new ProviderGroup("Azure", [throwingTranslator.Object]);
        var deepLGroup = new ProviderGroup("DeepL", [successfulTranslator.Object]);

        var roundRobin = new RoundRobinTranslator(
            [azureGroup, deepLGroup],
            _mockLogger.Object);

        // Act
        var result = await roundRobin.TranslateAsync("text", "cs");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Translation);
    }

    [Fact]
    public async Task TranslateAsync_KeyRotationWithinProvider()
    {
        // Arrange - 2 DeepL keys, no Azure (to test key rotation isolation)
        var deepL1 = CreateSuccessfulTranslator("T1", "DeepL-1");
        var deepL2 = CreateSuccessfulTranslator("T2", "DeepL-2");

        var deepLGroup = new ProviderGroup("DeepL", [deepL1.Object, deepL2.Object]);

        var roundRobin = new RoundRobinTranslator(
            [deepLGroup],
            _mockLogger.Object);

        // Act - 4 calls
        var results = new List<TranslatorResult>();
        for (int i = 0; i < 4; i++)
        {
            results.Add(await roundRobin.TranslateAsync("text", "cs"));
        }

        // Assert - Keys should alternate: 1, 2, 1, 2
        Assert.Equal("DeepL-1", results[0].Provider);
        Assert.Equal("DeepL-2", results[1].Provider);
        Assert.Equal("DeepL-1", results[2].Provider);
        Assert.Equal("DeepL-2", results[3].Provider);

        // Each called twice
        deepL1.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2));
        deepL2.Verify(t => t.TranslateAsync("text", "cs", null, default), Times.Exactly(2));
    }

    [Fact]
    public async Task TranslateAsync_LegacyConstructor_WorksAsSingleProvider()
    {
        // Arrange - Legacy flat list constructor
        var translator1 = CreateSuccessfulTranslator("T1", "P1");
        var translator2 = CreateSuccessfulTranslator("T2", "P2");

        var roundRobin = new RoundRobinTranslator(
            [translator1.Object, translator2.Object],
            _mockLogger.Object);

        // Act
        var result1 = await roundRobin.TranslateAsync("text1", "cs");
        var result2 = await roundRobin.TranslateAsync("text2", "cs");

        // Assert - Should rotate through translators
        Assert.NotEqual(result1.Provider, result2.Provider);
        Assert.Equal(1, roundRobin.ProviderCount); // Single "Default" group
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

    private static ProviderGroup CreateProviderGroup(string name, int translatorCount)
    {
        var translators = new List<ITranslator>();
        for (int i = 0; i < translatorCount; i++)
        {
            var mock = new Mock<ITranslator>();
            mock.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TranslatorResult.Ok($"Translation{i}", $"{name}", "en"));
            translators.Add(mock.Object);
        }
        return new ProviderGroup(name, translators);
    }
}
