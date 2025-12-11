using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Sync.Tests.Services;

public class EmbeddingTextBuilderTests
{
    [Fact]
    public void CreateEmbeddingText_WithTitleOnly_ReturnsTitle()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Issue Title";

        // Act
        var result = builder.CreateEmbeddingText(title, null);

        // Assert
        Assert.Equal(title, result);
    }

    [Fact]
    public void CreateEmbeddingText_WithEmptyBody_ReturnsTitle()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Issue Title";

        // Act
        var result = builder.CreateEmbeddingText(title, "");

        // Assert
        Assert.Equal(title, result);
    }

    [Fact]
    public void CreateEmbeddingText_WithWhitespaceBody_ReturnsTitle()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Issue Title";

        // Act
        var result = builder.CreateEmbeddingText(title, "   ");

        // Assert
        Assert.Equal(title, result);
    }

    [Fact]
    public void CreateEmbeddingText_WithTitleAndBody_ReturnsCombined()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body content";

        // Act
        var result = builder.CreateEmbeddingText(title, body);

        // Assert
        Assert.Equal("Test Title\n\nTest body content", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithLongContent_Truncates()
    {
        // Arrange
        var maxLength = 100;
        var builder = new EmbeddingTextBuilder(maxLength);
        var title = "Title";
        var body = new string('x', 200); // 200 characters

        // Act
        var result = builder.CreateEmbeddingText(title, body);

        // Assert
        Assert.Equal(maxLength, result.Length);
        Assert.StartsWith("Title\n\n", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithContentUnderLimit_DoesNotTruncate()
    {
        // Arrange
        var maxLength = 1000;
        var builder = new EmbeddingTextBuilder(maxLength);
        var title = "Short Title";
        var body = "Short body";

        // Act
        var result = builder.CreateEmbeddingText(title, body);

        // Assert
        Assert.Equal("Short Title\n\nShort body", result);
        Assert.True(result.Length < maxLength);
    }

    [Fact]
    public void CreateEmbeddingText_DefaultMaxLength_Is8000()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Title";
        var body = new string('x', 10000);

        // Act
        var result = builder.CreateEmbeddingText(title, body);

        // Assert
        Assert.Equal(8000, result.Length);
    }
}
