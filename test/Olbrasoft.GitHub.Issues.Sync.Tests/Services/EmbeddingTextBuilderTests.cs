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

    // ===== Tests for new overload with labels and comments =====

    [Fact]
    public void CreateEmbeddingText_WithLabels_IncludesLabelsSection()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";
        var labels = new List<string> { "bug", "enhancement" };

        // Act
        var result = builder.CreateEmbeddingText(title, body, labels, null);

        // Assert
        Assert.Contains("Labels: bug, enhancement", result);
        Assert.StartsWith("Test Title\n\nTest body\n\nLabels:", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithEmptyLabels_DoesNotIncludeLabelsSection()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";
        var labels = new List<string>();

        // Act
        var result = builder.CreateEmbeddingText(title, body, labels, null);

        // Assert
        Assert.DoesNotContain("Labels:", result);
        Assert.Equal("Test Title\n\nTest body", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithNullLabels_DoesNotIncludeLabelsSection()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";

        // Act
        var result = builder.CreateEmbeddingText(title, body, null, null);

        // Assert
        Assert.DoesNotContain("Labels:", result);
        Assert.Equal("Test Title\n\nTest body", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithComments_IncludesCommentsSection()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";
        var comments = new List<string> { "First comment", "Second comment" };

        // Act
        var result = builder.CreateEmbeddingText(title, body, null, comments);

        // Assert
        Assert.Contains("Comments:", result);
        Assert.Contains("First comment", result);
        Assert.Contains("Second comment", result);
        Assert.Contains("---", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithEmptyComments_DoesNotIncludeCommentsSection()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";
        var comments = new List<string>();

        // Act
        var result = builder.CreateEmbeddingText(title, body, null, comments);

        // Assert
        Assert.DoesNotContain("Comments:", result);
        Assert.Equal("Test Title\n\nTest body", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithWhitespaceOnlyComments_FiltersThemOut()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";
        var comments = new List<string> { "Valid comment", "   ", "", null! };

        // Act
        var result = builder.CreateEmbeddingText(title, body, null, comments);

        // Assert
        Assert.Contains("Comments:", result);
        Assert.Contains("Valid comment", result);
        // Should only have one separator for the valid comment
        Assert.Equal(1, result.Split("---").Length - 1);
    }

    [Fact]
    public void CreateEmbeddingText_WithLabelsAndComments_IncludesBoth()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Bug Report";
        var body = "Something is broken";
        var labels = new List<string> { "bug", "priority:high" };
        var comments = new List<string> { "I can reproduce this", "Fixed in PR #42" };

        // Act
        var result = builder.CreateEmbeddingText(title, body, labels, comments);

        // Assert
        Assert.StartsWith("Bug Report", result);
        Assert.Contains("Something is broken", result);
        Assert.Contains("Labels: bug, priority:high", result);
        Assert.Contains("Comments:", result);
        Assert.Contains("I can reproduce this", result);
        Assert.Contains("Fixed in PR #42", result);
    }

    [Fact]
    public void CreateEmbeddingText_WithAllContent_MaintainsCorrectOrder()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Title";
        var body = "Body";
        var labels = new List<string> { "label1" };
        var comments = new List<string> { "comment1" };

        // Act
        var result = builder.CreateEmbeddingText(title, body, labels, comments);

        // Assert - verify order: Title, Body, Labels, Comments
        var titleIndex = result.IndexOf("Title", StringComparison.Ordinal);
        var bodyIndex = result.IndexOf("Body", StringComparison.Ordinal);
        var labelsIndex = result.IndexOf("Labels:", StringComparison.Ordinal);
        var commentsIndex = result.IndexOf("Comments:", StringComparison.Ordinal);

        Assert.True(titleIndex < bodyIndex, "Title should come before body");
        Assert.True(bodyIndex < labelsIndex, "Body should come before labels");
        Assert.True(labelsIndex < commentsIndex, "Labels should come before comments");
    }

    [Fact]
    public void CreateEmbeddingText_WithAllContent_TruncatesWhenOverLimit()
    {
        // Arrange
        var maxLength = 100;
        var builder = new EmbeddingTextBuilder(maxLength);
        var title = "Title";
        var body = "Body";
        var labels = new List<string> { "bug", "enhancement" };
        var comments = new List<string> { new string('c', 200) }; // Very long comment

        // Act
        var result = builder.CreateEmbeddingText(title, body, labels, comments);

        // Assert
        Assert.Equal(maxLength, result.Length);
    }

    [Fact]
    public void CreateEmbeddingText_SimpleOverload_DelegatesToFullOverload()
    {
        // Arrange
        var builder = new EmbeddingTextBuilder();
        var title = "Test Title";
        var body = "Test body";

        // Act
        var simpleResult = builder.CreateEmbeddingText(title, body);
        var fullResult = builder.CreateEmbeddingText(title, body, null, null);

        // Assert
        Assert.Equal(simpleResult, fullResult);
    }
}
