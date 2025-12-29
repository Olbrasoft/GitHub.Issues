using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class TranslationFallbackServiceTests
{
    private readonly Mock<ITranslator> _primaryTranslatorMock;
    private readonly Mock<ITranslator> _fallbackTranslatorMock;
    private readonly Mock<ILogger<TranslationFallbackService>> _loggerMock;

    public TranslationFallbackServiceTests()
    {
        _primaryTranslatorMock = new Mock<ITranslator>();
        _fallbackTranslatorMock = new Mock<ITranslator>();
        _loggerMock = new Mock<ILogger<TranslationFallbackService>>();
    }

    private TranslationFallbackService CreateServiceWithFallback()
    {
        return new TranslationFallbackService(
            _primaryTranslatorMock.Object,
            _loggerMock.Object,
            _fallbackTranslatorMock.Object);
    }

    private TranslationFallbackService CreateServiceWithoutFallback()
    {
        return new TranslationFallbackService(
            _primaryTranslatorMock.Object,
            _loggerMock.Object,
            fallbackTranslator: null);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenEmptyText_ReturnsFailure()
    {
        // Arrange
        var service = CreateServiceWithFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("", "cs", "en");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Empty", result.Error);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenPrimarySucceeds_ReturnsTranslation()
    {
        // Arrange
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Ahoj",
                Provider = "Azure"
            });

        var service = CreateServiceWithFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("Hello", "cs", "en");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Ahoj", result.Translation);
        Assert.Equal("Azure", result.Provider);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenPrimaryFailsAndFallbackSucceeds_UsesFallback()
    {
        // Arrange
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "Primary failed" });

        _fallbackTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Ahoj",
                Provider = "DeepL"
            });

        var service = CreateServiceWithFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("Hello", "cs", "en");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Ahoj", result.Translation);
        Assert.Equal("DeepL", result.Provider);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenPrimaryFailsAndNoFallback_ReturnsFailure()
    {
        // Arrange
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "Primary failed" });

        var service = CreateServiceWithoutFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("Hello", "cs", "en");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.UsedFallback);
        Assert.Contains("Primary failed", result.Error);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenBothFail_ReturnsFailure()
    {
        // Arrange
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "Primary failed" });

        _fallbackTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult { Success = false, Error = "Fallback failed" });

        var service = CreateServiceWithFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("Hello", "cs", "en");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.UsedFallback);
        Assert.Contains("Fallback failed", result.Error);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_WhenPrimaryReturnsEmptyTranslation_TriesFallback()
    {
        // Arrange
        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "", // Empty translation
                Provider = "Azure"
            });

        _fallbackTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Ahoj",
                Provider = "DeepL"
            });

        var service = CreateServiceWithFallback();

        // Act
        var result = await service.TranslateWithFallbackAsync("Hello", "cs", "en");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("DeepL", result.Provider);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task TranslateWithFallbackAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _primaryTranslatorMock
            .Setup(x => x.TranslateAsync("Hello", "cs", "en", token))
            .ReturnsAsync(new TranslatorResult
            {
                Success = true,
                Translation = "Ahoj",
                Provider = "Azure"
            });

        var service = CreateServiceWithFallback();

        // Act
        await service.TranslateWithFallbackAsync("Hello", "cs", "en", token);

        // Assert
        _primaryTranslatorMock.Verify(x => x.TranslateAsync("Hello", "cs", "en", token), Times.Once);
    }
}
