using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Tests.Entities;

public class IssueTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.Equal(0, issue.Id);
        Assert.Equal(0, issue.RepositoryId);
        Assert.Equal(0, issue.Number);
        Assert.Equal(string.Empty, issue.Title);
        Assert.True(issue.IsOpen);
        Assert.Equal(string.Empty, issue.Url);
        Assert.Null(issue.ParentIssueId);
        Assert.Null(issue.ParentIssue);
        Assert.NotNull(issue.IssueLabels);
        Assert.Empty(issue.IssueLabels);
        Assert.NotNull(issue.Events);
        Assert.Empty(issue.Events);
        Assert.NotNull(issue.SubIssues);
        Assert.Empty(issue.SubIssues);
    }

    [Fact]
    public void CanSetProperties()
    {
        // Arrange
        var issue = new Issue
        {
            Id = 1,
            RepositoryId = 10,
            Number = 42,
            Title = "Test Issue",
            IsOpen = false,
            Url = "https://github.com/test/repo/issues/42"
        };

        // Assert
        Assert.Equal(1, issue.Id);
        Assert.Equal(10, issue.RepositoryId);
        Assert.Equal(42, issue.Number);
        Assert.Equal("Test Issue", issue.Title);
        Assert.False(issue.IsOpen);
        Assert.Equal("https://github.com/test/repo/issues/42", issue.Url);
    }

    [Fact]
    public void ParentChildRelationship_CanBeSet()
    {
        // Arrange
        var parentIssue = new Issue { Id = 1, Number = 1, Title = "Parent" };
        var childIssue = new Issue
        {
            Id = 2,
            Number = 2,
            Title = "Child",
            ParentIssueId = 1,
            ParentIssue = parentIssue
        };

        // Act
        parentIssue.SubIssues.Add(childIssue);

        // Assert
        Assert.Equal(1, childIssue.ParentIssueId);
        Assert.Same(parentIssue, childIssue.ParentIssue);
        Assert.Contains(childIssue, parentIssue.SubIssues);
    }
}
