using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

namespace Olbrasoft.GitHub.Issues.Tests.Services;

public class OllamaEmbeddingServiceTests
{
    private readonly Mock<ILogger<OllamaEmbeddingService>> _loggerMock;
    private readonly EmbeddingSettings _settings;

    public OllamaEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<OllamaEmbeddingService>>();
        _settings = new EmbeddingSettings
        {
            BaseUrl = "http://localhost:11434",
            Model = "nomic-embed-text"
        };
    }

    private OllamaEmbeddingService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(_settings);
        return new OllamaEmbeddingService(httpClient, options, _loggerMock.Object);
    }

    private Mock<HttpMessageHandler> CreateMockHandler(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return handlerMock;
    }

    [Fact]
    public void IsConfigured_WhenBaseUrlSet_ReturnsTrue()
    {
        // Arrange
        var handler = CreateMockHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler.Object);

        // Act
        var result = service.IsConfigured;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Constructor_WhenBaseUrlEmpty_ThrowsUriFormatException()
    {
        // Arrange
        _settings.BaseUrl = "";
        var handler = CreateMockHandler(new HttpResponseMessage(HttpStatusCode.OK));

        // Act & Assert
        Assert.Throws<UriFormatException>(() => CreateService(handler.Object));
    }

    [Fact]
    public async Task IsAvailableAsync_WhenOllamaResponds_ReturnsTrue()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var handler = CreateMockHandler(response);
        var service = CreateService(handler.Object);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenOllamaNotRunning_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var service = CreateService(handlerMock.Object);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var handler = CreateMockHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithWhitespaceText_ReturnsNull()
    {
        // Arrange
        var handler = CreateMockHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ReturnsVector()
    {
        // Arrange
        var embeddingArray = new float[768];
        for (int i = 0; i < 768; i++) embeddingArray[i] = 0.1f * i;

        var responseContent = JsonSerializer.Serialize(new { embedding = embeddingArray });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };
        var handler = CreateMockHandler(response);
        var service = CreateService(handler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(768, result.ToArray().Length);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenApiReturnsError_ReturnsNull()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handler = CreateMockHandler(response);
        var service = CreateService(handler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenResponseMissingEmbedding_ReturnsNull()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new { other = "data" });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };
        var handler = CreateMockHandler(response);
        var service = CreateService(handler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenConnectionFails_ReturnsNull()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var service = CreateService(handlerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void OllamaEmbeddingService_ImplementsIEmbeddingService()
    {
        // Verify implementation follows ISP - implements core embedding interface
        Assert.True(typeof(IEmbeddingService).IsAssignableFrom(typeof(OllamaEmbeddingService)));
    }

    [Fact]
    public void OllamaEmbeddingService_ImplementsIServiceLifecycleManager()
    {
        // Verify implementation follows ISP - implements lifecycle management interface
        Assert.True(typeof(IServiceLifecycleManager).IsAssignableFrom(typeof(OllamaEmbeddingService)));
    }

    [Fact]
    public void IServiceLifecycleManager_CanBeMocked()
    {
        // Verify interface can be mocked independently
        var mock = new Mock<IServiceLifecycleManager>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public async Task IServiceLifecycleManager_EnsureRunningAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IServiceLifecycleManager>();
        mock.Setup(x => x.EnsureRunningAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.EnsureRunningAsync();

        // Assert
        mock.Verify(x => x.EnsureRunningAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
