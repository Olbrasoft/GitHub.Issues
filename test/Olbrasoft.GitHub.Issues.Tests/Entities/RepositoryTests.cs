using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Tests.Entities;

public class RepositoryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var repository = new Repository();

        // Assert
        Assert.Equal(0, repository.Id);
        Assert.Equal(0, repository.GitHubId);
        Assert.Equal(string.Empty, repository.FullName);
        Assert.Equal(string.Empty, repository.HtmlUrl);
        Assert.Null(repository.LastSyncedAt);
        Assert.NotNull(repository.Issues);
        Assert.Empty(repository.Issues);
        Assert.NotNull(repository.Labels);
        Assert.Empty(repository.Labels);
    }

    [Fact]
    public void CanSetProperties()
    {
        // Arrange
        var syncTime = DateTimeOffset.UtcNow;
        var repository = new Repository
        {
            Id = 1,
            GitHubId = 123456789,
            FullName = "Olbrasoft/GitHub.Issues",
            HtmlUrl = "https://github.com/Olbrasoft/GitHub.Issues",
            LastSyncedAt = syncTime
        };

        // Assert
        Assert.Equal(1, repository.Id);
        Assert.Equal(123456789, repository.GitHubId);
        Assert.Equal("Olbrasoft/GitHub.Issues", repository.FullName);
        Assert.Equal("https://github.com/Olbrasoft/GitHub.Issues", repository.HtmlUrl);
        Assert.Equal(syncTime, repository.LastSyncedAt);
    }

    [Fact]
    public void CanAddIssues()
    {
        // Arrange
        var repository = new Repository { Id = 1, FullName = "test/repo" };
        var issue = new Issue { Id = 1, Number = 42, Title = "Test Issue" };

        // Act
        repository.Issues.Add(issue);

        // Assert
        Assert.Single(repository.Issues);
        Assert.Contains(issue, repository.Issues);
    }

    [Fact]
    public void CanAddLabels()
    {
        // Arrange
        var repository = new Repository { Id = 1, FullName = "test/repo" };
        var label = new Label { Id = 1, Name = "bug" };

        // Act
        repository.Labels.Add(label);

        // Assert
        Assert.Single(repository.Labels);
        Assert.Contains(label, repository.Labels);
    }
}
