using Olbrasoft.GitHub.Issues.Business.Detail;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class BodyPreviewGeneratorTests
{
    private readonly BodyPreviewGenerator _generator = new();

    [Fact]
    public void CreatePreview_WhenBodyIsNull_ReturnsEmpty()
    {
        // Act
        var result = _generator.CreatePreview(null!, 100);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CreatePreview_WhenBodyIsEmpty_ReturnsEmpty()
    {
        // Act
        var result = _generator.CreatePreview("", 100);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CreatePreview_WhenBodyIsShorterThanMax_ReturnsFullText()
    {
        // Arrange
        const string body = "Short text";

        // Act
        var result = _generator.CreatePreview(body, 100);

        // Assert
        Assert.Equal("Short text", result);
    }

    [Fact]
    public void CreatePreview_WhenBodyIsLongerThanMax_TruncatesWithEllipsis()
    {
        // Arrange
        const string body = "This is a longer text that needs to be truncated because it exceeds the maximum length";

        // Act
        var result = _generator.CreatePreview(body, 30);

        // Assert
        Assert.EndsWith("...", result);
        Assert.True(result.Length <= 33); // 30 + "..."
    }

    [Fact]
    public void CreatePreview_RemovesCodeBlocks()
    {
        // Arrange
        const string body = "Before code ```csharp\nvar x = 1;\n``` after code";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("```", result);
        Assert.DoesNotContain("var x", result);
        Assert.Contains("Before code", result);
        Assert.Contains("after code", result);
    }

    [Fact]
    public void CreatePreview_RemovesInlineCode()
    {
        // Arrange
        const string body = "Use `Console.WriteLine` to print";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("`", result);
        Assert.DoesNotContain("Console.WriteLine", result);
        Assert.Contains("Use", result);
        Assert.Contains("to print", result);
    }

    [Fact]
    public void CreatePreview_RemovesHeaders()
    {
        // Arrange
        const string body = "# Header 1\n## Header 2\nNormal text";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("#", result);
        Assert.Contains("Header 1", result);
        Assert.Contains("Header 2", result);
        Assert.Contains("Normal text", result);
    }

    [Fact]
    public void CreatePreview_KeepsLinkTextRemovesUrl()
    {
        // Arrange
        const string body = "Check [this link](https://example.com) for more info";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("https://example.com", result);
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
        Assert.Contains("this link", result);
    }

    [Fact]
    public void CreatePreview_RemovesImages()
    {
        // Arrange
        const string body = "Text before ![alt text](image.png) text after";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("![", result);
        Assert.DoesNotContain("image.png", result);
        Assert.Contains("Text before", result);
        Assert.Contains("text after", result);
    }

    [Fact]
    public void CreatePreview_RemovesBoldAndItalic()
    {
        // Arrange
        const string body = "Text with **bold** and *italic* formatting";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("**", result);
        Assert.DoesNotContain("*", result);
        Assert.Contains("bold", result);
        Assert.Contains("italic", result);
    }

    [Fact]
    public void CreatePreview_RemovesBlockquotes()
    {
        // Arrange
        const string body = "> This is a quote\nNormal text";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain(">", result);
        Assert.Contains("This is a quote", result);
        Assert.Contains("Normal text", result);
    }

    [Fact]
    public void CreatePreview_NormalizesWhitespace()
    {
        // Arrange
        const string body = "Text   with    multiple    spaces\n\nand\nnewlines";

        // Act
        var result = _generator.CreatePreview(body, 500);

        // Assert
        Assert.DoesNotContain("  ", result); // No double spaces
        Assert.Contains("Text with multiple spaces and newlines", result);
    }
}
