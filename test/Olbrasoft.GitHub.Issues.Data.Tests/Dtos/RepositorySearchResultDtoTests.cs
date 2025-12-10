using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Data.Tests.Dtos;

public class RepositorySearchResultDtoTests
{
    [Fact]
    public void RepositorySearchResultDto_HasCorrectDefaults()
    {
        // Act
        var dto = new RepositorySearchResultDto();

        // Assert
        Assert.Equal(0, dto.Id);
        Assert.Equal(string.Empty, dto.FullName);
    }

    [Fact]
    public void RepositorySearchResultDto_CanSetProperties()
    {
        // Arrange & Act
        var dto = new RepositorySearchResultDto
        {
            Id = 42,
            FullName = "Olbrasoft/VirtualAssistant"
        };

        // Assert
        Assert.Equal(42, dto.Id);
        Assert.Equal("Olbrasoft/VirtualAssistant", dto.FullName);
    }
}
