using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.Tests;

public class EmbeddingProviderTests
{
    [Fact]
    public void Ollama_HasValue0()
    {
        Assert.Equal(0, (int)EmbeddingProvider.Ollama);
    }

    [Fact]
    public void Cohere_HasValue1()
    {
        Assert.Equal(1, (int)EmbeddingProvider.Cohere);
    }

    [Fact]
    public void Enum_HasTwoValues()
    {
        var values = Enum.GetValues<EmbeddingProvider>();
        Assert.Equal(2, values.Length);
    }

    [Theory]
    [InlineData("Ollama", EmbeddingProvider.Ollama)]
    [InlineData("Cohere", EmbeddingProvider.Cohere)]
    public void Parse_ReturnsCorrectValue(string name, EmbeddingProvider expected)
    {
        var result = Enum.Parse<EmbeddingProvider>(name);
        Assert.Equal(expected, result);
    }
}
