using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Text.Transformation.Ollama.Tests;

public class OllamaEmbeddingServiceTests
{
    private readonly Mock<IServiceManager> _serviceManagerMock;
    private readonly Mock<ILogger<OllamaEmbeddingService>> _loggerMock;

    public OllamaEmbeddingServiceTests()
    {
        _serviceManagerMock = new Mock<IServiceManager>();
        _loggerMock = new Mock<ILogger<OllamaEmbeddingService>>();
    }

    private static IOptions<EmbeddingSettings> CreateSettings()
    {
        return Options.Create(new EmbeddingSettings
        {
            Provider = EmbeddingProvider.Ollama,
            OllamaBaseUrl = "http://localhost:11434",
            OllamaModel = "nomic-embed-text",
            Dimensions = 768,
            MaxStartupRetries = 3,
            StartupRetryDelayMs = 100
        });
    }

    private static HttpClient CreateMockedHttpClient(
        HttpStatusCode statusCode,
        string responseContent,
        string baseUrl = "http://localhost:11434")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri(baseUrl) };
    }

    [Fact]
    public void IsConfigured_WithBaseUrl_ReturnsTrue()
    {
        var settings = CreateSettings();
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsNull()
    {
        var settings = CreateSettings();
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithWhitespace_ReturnsNull()
    {
        var settings = CreateSettings();
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("   ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithSuccessfulResponse_ReturnsEmbedding()
    {
        var settings = CreateSettings();
        var responseJson = JsonSerializer.Serialize(new
        {
            embedding = new[] { 0.1f, 0.2f, 0.3f }
        });
        var httpClient = CreateMockedHttpClient(HttpStatusCode.OK, responseJson);
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("test text");

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(0.1f, result[0]);
        Assert.Equal(0.2f, result[1]);
        Assert.Equal(0.3f, result[2]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithErrorResponse_ReturnsNull()
    {
        var settings = CreateSettings();
        var httpClient = CreateMockedHttpClient(HttpStatusCode.InternalServerError, "Error");
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("test text");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithMissingEmbeddingProperty_ReturnsNull()
    {
        var settings = CreateSettings();
        var responseJson = JsonSerializer.Serialize(new { other = "value" });
        var httpClient = CreateMockedHttpClient(HttpStatusCode.OK, responseJson);
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("test text");

        Assert.Null(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiSucceeds_ReturnsTrue()
    {
        var settings = CreateSettings();
        var httpClient = CreateMockedHttpClient(HttpStatusCode.OK, "{\"models\":[]}");
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiFails_ReturnsFalse()
    {
        var settings = CreateSettings();
        var httpClient = CreateMockedHttpClient(HttpStatusCode.ServiceUnavailable, "");
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.IsAvailableAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task EnsureRunningAsync_WhenAlreadyRunning_DoesNotStartService()
    {
        var settings = CreateSettings();
        var httpClient = CreateMockedHttpClient(HttpStatusCode.OK, "{\"models\":[]}");
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        await service.EnsureRunningAsync();

        _serviceManagerMock.Verify(
            m => m.StartServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(EmbeddingInputType.Document)]
    [InlineData(EmbeddingInputType.Query)]
    public async Task GenerateEmbeddingAsync_IgnoresInputType_ReturnsEmbedding(EmbeddingInputType inputType)
    {
        var settings = CreateSettings();
        var responseJson = JsonSerializer.Serialize(new
        {
            embedding = new[] { 0.5f, 0.6f }
        });
        var httpClient = CreateMockedHttpClient(HttpStatusCode.OK, responseJson);
        var service = new OllamaEmbeddingService(httpClient, _serviceManagerMock.Object, settings, _loggerMock.Object);

        var result = await service.GenerateEmbeddingAsync("test text", inputType);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
    }
}
