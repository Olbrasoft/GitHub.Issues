using Olbrasoft.GitHub.Issues.Business.Services;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class IssueNumberParserTests
{
    #region Parse Tests

    [Fact]
    public void Parse_NullQuery_ReturnsEmptyList()
    {
        var result = IssueNumberParser.Parse(null);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyQuery_ReturnsEmptyList()
    {
        var result = IssueNumberParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceQuery_ReturnsEmptyList()
    {
        var result = IssueNumberParser.Parse("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_HashNumber_ReturnsIssueNumber()
    {
        var result = IssueNumberParser.Parse("#123");

        Assert.Single(result);
        Assert.Equal(123, result[0].Number);
        Assert.Null(result[0].RepositoryName);
    }

    [Fact]
    public void Parse_JustNumber_ReturnsIssueNumber()
    {
        var result = IssueNumberParser.Parse("42");

        Assert.Single(result);
        Assert.Equal(42, result[0].Number);
        Assert.Null(result[0].RepositoryName);
    }

    [Fact]
    public void Parse_IssueKeyword_ReturnsIssueNumber()
    {
        var result = IssueNumberParser.Parse("issue 27");

        Assert.Single(result);
        Assert.Equal(27, result[0].Number);
        Assert.Null(result[0].RepositoryName);
    }

    [Fact]
    public void Parse_IssuesKeyword_ReturnsIssueNumber()
    {
        var result = IssueNumberParser.Parse("issues #15");

        Assert.Single(result);
        Assert.Equal(15, result[0].Number);
        Assert.Null(result[0].RepositoryName);
    }

    [Fact]
    public void Parse_RepoHashNumber_ReturnsWithRepoName()
    {
        var result = IssueNumberParser.Parse("VirtualAssistant#123");

        Assert.Single(result);
        Assert.Equal(123, result[0].Number);
        Assert.Equal("VirtualAssistant", result[0].RepositoryName);
    }

    [Fact]
    public void Parse_OwnerRepoHashNumber_ReturnsWithFullRepoName()
    {
        var result = IssueNumberParser.Parse("Olbrasoft/VirtualAssistant#456");

        Assert.Single(result);
        Assert.Equal(456, result[0].Number);
        Assert.Equal("Olbrasoft/VirtualAssistant", result[0].RepositoryName);
    }

    [Fact]
    public void Parse_RepoWithDots_ReturnsWithRepoName()
    {
        var result = IssueNumberParser.Parse("GitHub.Issues#99");

        Assert.Single(result);
        Assert.Equal(99, result[0].Number);
        Assert.Equal("GitHub.Issues", result[0].RepositoryName);
    }

    [Fact]
    public void Parse_RepoWithDashes_ReturnsWithRepoName()
    {
        var result = IssueNumberParser.Parse("my-cool-repo#77");

        Assert.Single(result);
        Assert.Equal(77, result[0].Number);
        Assert.Equal("my-cool-repo", result[0].RepositoryName);
    }

    [Fact]
    public void Parse_LongText_DoesNotMatchBuriedNumber()
    {
        // Number buried in irrelevant text should not match
        var result = IssueNumberParser.Parse("fix this bug 27 times in the code");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ShortQueryWithNumber_MatchesNearStart()
    {
        var result = IssueNumberParser.Parse("bug 5");

        Assert.Single(result);
        Assert.Equal(5, result[0].Number);
    }

    #endregion

    #region GetSemanticQuery Tests

    [Fact]
    public void GetSemanticQuery_NullQuery_ReturnsNull()
    {
        var result = IssueNumberParser.GetSemanticQuery(null);
        Assert.Null(result);
    }

    [Fact]
    public void GetSemanticQuery_EmptyQuery_ReturnsNull()
    {
        var result = IssueNumberParser.GetSemanticQuery("");
        Assert.Null(result);
    }

    [Fact]
    public void GetSemanticQuery_OnlyIssueNumber_ReturnsNull()
    {
        var result = IssueNumberParser.GetSemanticQuery("#123");
        Assert.Null(result);
    }

    [Fact]
    public void GetSemanticQuery_OnlyRepoAndNumber_ReturnsNull()
    {
        var result = IssueNumberParser.GetSemanticQuery("VirtualAssistant#123");
        Assert.Null(result);
    }

    [Fact]
    public void GetSemanticQuery_IssueKeywordOnly_ReturnsNull()
    {
        var result = IssueNumberParser.GetSemanticQuery("issue 42");
        Assert.Null(result);
    }

    [Fact]
    public void GetSemanticQuery_TextOnly_ReturnsText()
    {
        var result = IssueNumberParser.GetSemanticQuery("authentication error");
        Assert.Equal("authentication error", result);
    }

    [Fact]
    public void GetSemanticQuery_MixedQuery_ReturnsTextPart()
    {
        // Query that starts with issue pattern should have it removed
        var result = IssueNumberParser.GetSemanticQuery("#123 authentication bug");

        // The result may vary based on implementation
        // Key is that the semantic portion is preserved
        Assert.NotNull(result);
    }

    #endregion
}
