using Microsoft.Extensions.Configuration;
using Olbrasoft.GitHub.Issues.Configuration;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Tests.Configuration;

/// <summary>
/// Unit tests for ConfigurationKeyLoader.LoadNumberedKeys method.
/// </summary>
public class ConfigurationKeyLoaderTests
{
    [Fact]
    public void LoadNumberedKeys_NoKeys_ReturnsEmptyList()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void LoadNumberedKeys_SingleKey_ReturnsSingleKey()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Single(result);
        Assert.Equal("value1", result[0]);
    }

    [Fact]
    public void LoadNumberedKeys_MultipleKeys_ReturnsAllKeys()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1",
                ["TestPrefix:Key2"] = "value2",
                ["TestPrefix:Key3"] = "value3"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result[0]);
        Assert.Equal("value2", result[1]);
        Assert.Equal("value3", result[2]);
    }

    [Fact]
    public void LoadNumberedKeys_GapInSequence_StopsAtGap()
    {
        // Arrange - Key2 is missing, so we should only get Key1
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1",
                ["TestPrefix:Key3"] = "value3"  // Key2 is missing
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Single(result);
        Assert.Equal("value1", result[0]);
    }

    [Fact]
    public void LoadNumberedKeys_EmptyValue_StopsAtEmptyValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1",
                ["TestPrefix:Key2"] = "",  // Empty string
                ["TestPrefix:Key3"] = "value3"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Single(result);
        Assert.Equal("value1", result[0]);
    }

    [Fact]
    public void LoadNumberedKeys_WhitespaceValue_StopsAtWhitespaceValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1",
                ["TestPrefix:Key2"] = "   ",  // Whitespace only
                ["TestPrefix:Key3"] = "value3"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Single(result);
        Assert.Equal("value1", result[0]);
    }

    [Fact]
    public void LoadNumberedKeys_NullValue_StopsAtNullValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestPrefix:Key1"] = "value1",
                ["TestPrefix:Key2"] = null,  // Null value
                ["TestPrefix:Key3"] = "value3"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Single(result);
        Assert.Equal("value1", result[0]);
    }

    [Fact]
    public void LoadNumberedKeys_DifferentPrefix_DoesNotFindKeys()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Other:Key1"] = "value1",
                ["Other:Key2"] = "value2"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void LoadNumberedKeys_RealisticAzureApiKeys_LoadsAllKeys()
    {
        // Arrange - Realistic scenario for Azure API keys
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TranslatorPool:AzureApiKey1"] = "azure-key-1",
                ["TranslatorPool:AzureApiKey2"] = "azure-key-2"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TranslatorPool:AzureApiKey");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("azure-key-1", result[0]);
        Assert.Equal("azure-key-2", result[1]);
    }

    [Fact]
    public void LoadNumberedKeys_RealisticCohereApiKeys_LoadsAllKeys()
    {
        // Arrange - Realistic scenario for Cohere API keys
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:Cohere:Key1"] = "cohere-key-1",
                ["AiProviders:Cohere:Key2"] = "cohere-key-2",
                ["AiProviders:Cohere:Key3"] = "cohere-key-3"
            })
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "AiProviders:Cohere:Key");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("cohere-key-1", result[0]);
        Assert.Equal("cohere-key-2", result[1]);
        Assert.Equal("cohere-key-3", result[2]);
    }

    [Fact]
    public void LoadNumberedKeys_ManyKeys_LoadsAllKeys()
    {
        // Arrange - Test with many keys to ensure no arbitrary limits
        var configData = new Dictionary<string, string?>();
        for (var i = 1; i <= 20; i++)
        {
            configData[$"TestPrefix:Key{i}"] = $"value{i}";
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = ConfigurationKeyLoader.LoadNumberedKeys(config, "TestPrefix:Key");

        // Assert
        Assert.Equal(20, result.Count);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal($"value{i + 1}", result[i]);
        }
    }
}
