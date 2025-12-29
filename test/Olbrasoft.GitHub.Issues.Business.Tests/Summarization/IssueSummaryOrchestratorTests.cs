using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Business.Translation;
using Olbrasoft.GitHub.Issues.Data;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Repositories;
using Xunit;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Summarization;

public class IssueSummaryOrchestratorTests
{
    private readonly Mock<ICachedTextRepository> _mockRepository;
    private readonly Mock<ISummaryCacheService> _mockCacheService;
    private readonly Mock<IAiSummarizationService> _mockAiService;
    private readonly Mock<ITranslationFallbackService> _mockTranslationService;
    private readonly Mock<ISummaryNotificationService> _mockNotificationService;
    private readonly Mock<IGitHubGraphQLClient> _mockGraphQLClient;
    private readonly Mock<IIssueDetailQueryService> _mockQueryService;
    private readonly Mock<ILogger<IssueSummaryOrchestrator>> _mockLogger;
    private readonly IssueSummaryOrchestrator _orchestrator;
    private readonly DateTime _testTime;

    public IssueSummaryOrchestratorTests()
    {
        _mockRepository = new Mock<ICachedTextRepository>();
        _mockCacheService = new Mock<ISummaryCacheService>();
        _mockAiService = new Mock<IAiSummarizationService>();
        _mockTranslationService = new Mock<ITranslationFallbackService>();
        _mockNotificationService = new Mock<ISummaryNotificationService>();
        _mockGraphQLClient = new Mock<IGitHubGraphQLClient>();
        _mockQueryService = new Mock<IIssueDetailQueryService>();
        _mockLogger = new Mock<ILogger<IssueSummaryOrchestrator>>();

        _testTime = new DateTime(2025, 12, 29, 12, 0, 0, DateTimeKind.Utc);

        _orchestrator = new IssueSummaryOrchestrator(
            _mockRepository.Object,
            _mockCacheService.Object,
            _mockAiService.Object,
            _mockTranslationService.Object,
            _mockNotificationService.Object,
            _mockGraphQLClient.Object,
            _mockQueryService.Object,
            _mockLogger.Object);
    }

    #region GenerateSummaryFromBodyAsync Tests

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenIssueNotFound_ReturnsEarly()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Test body";

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenEnglishCacheHit_ServesFromCache()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Test body";
        const string cachedSummary = "Cached English summary";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                issueId,
                (int)LanguageCode.EnUS,
                issue.GitHubUpdatedAt.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedSummary);

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, cachedSummary, "cache", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenBothLanguagesCached_ServesBothFromCache()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Test body";
        const string cachedEn = "Cached English";
        const string cachedCs = "Cached Czech";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                issueId,
                (int)LanguageCode.EnUS,
                issue.GitHubUpdatedAt.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEn);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                issueId,
                (int)LanguageCode.CsCZ,
                issue.GitHubUpdatedAt.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedCs);

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "both");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, cachedEn, "cache", "en", It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, cachedCs, "cache", "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenBothLanguagesWithEmptyCaches_GeneratesAndTranslates()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body content";
        const string enSummary = "English summary";
        const string csSummary = "Český souhrn";
        const string provider = "Cerebras/llama3.1-8b";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        // Empty caches
        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: enSummary,
                Provider: provider,
                Error: null));

        _mockTranslationService.Setup(s => s.TranslateWithFallbackAsync(
                enSummary,
                "cs",
                "en",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(
                Success: true,
                Translation: csSummary,
                Provider: "DeepL",
                UsedFallback: false,
                Error: null));

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "both");

        // Assert
        // AI summarization called
        _mockAiService.Verify(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()), Times.Once);

        // Translation called
        _mockTranslationService.Verify(s => s.TranslateWithFallbackAsync(
            enSummary, "cs", "en", It.IsAny<CancellationToken>()), Times.Once);

        // Both EN and CS cached
        _mockCacheService.Verify(s => s.SaveAsync(
            issueId,
            (int)LanguageCode.EnUS,
            enSummary,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(s => s.SaveAsync(
            issueId,
            (int)LanguageCode.CsCZ,
            csSummary,
            It.IsAny<CancellationToken>()), Times.Once);

        // Both EN and CS notifications sent
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, enSummary, provider, "en", It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, csSummary, $"{provider} → DeepL", "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenEmptyBody_ReturnsEarly()
    {
        // Arrange
        const int issueId = 123;
        const string emptyBody = "   ";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, emptyBody, "en");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenCacheMiss_GeneratesAiSummary()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body content";
        const string aiSummary = "AI generated summary";
        const string provider = "Cerebras/llama3.1-8b";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: aiSummary,
                Provider: provider,
                Error: null));

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(s => s.SaveAsync(
            issueId,
            (int)LanguageCode.EnUS,
            aiSummary,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, aiSummary, provider, "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenAiFails_ReturnsEarly()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: false,
                Summary: null,
                Provider: "Cerebras",
                Error: "Rate limit exceeded"));

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "en");

        // Assert
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCacheService.Verify(s => s.SaveAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenCzechRequested_TranslatesEnglishSummary()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body";
        const string enSummary = "English summary";
        const string csSummary = "Český souhrn";
        const string provider = "Cerebras/llama3.1-8b";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: enSummary,
                Provider: provider,
                Error: null));

        _mockTranslationService.Setup(s => s.TranslateWithFallbackAsync(
                enSummary,
                "cs",
                "en",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(
                Success: true,
                Translation: csSummary,
                Provider: "DeepL",
                UsedFallback: false,
                Error: null));

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert
        _mockTranslationService.Verify(s => s.TranslateWithFallbackAsync(
            enSummary, "cs", "en", It.IsAny<CancellationToken>()), Times.Once);

        // Verify both EN and CS cache saves
        _mockCacheService.Verify(s => s.SaveAsync(
            issueId,
            (int)LanguageCode.EnUS,
            enSummary,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(s => s.SaveAsync(
            issueId,
            (int)LanguageCode.CsCZ,
            csSummary,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify only CS notification sent (EN not sent when language=cs)
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, enSummary, It.IsAny<string>(), "en", It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, csSummary, $"{provider} → DeepL", "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryFromBodyAsync_WhenTranslationFails_SendsEnglishFallback()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Issue body";
        const string enSummary = "English summary";
        const string provider = "Cerebras/llama3.1-8b";
        var issue = CreateTestIssue(issueId);

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: enSummary,
                Provider: provider,
                Error: null));

        _mockTranslationService.Setup(s => s.TranslateWithFallbackAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(
                Success: false,
                Translation: null,
                Provider: "DeepL",
                UsedFallback: false,
                Error: "Translation failed"));

        // Act
        await _orchestrator.GenerateSummaryFromBodyAsync(issueId, body, "cs");

        // Assert
        _mockNotificationService.Verify(s => s.NotifySummaryAsync(
            issueId, enSummary, provider + " (EN fallback)", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GenerateSummaryAsync(int issueId, string language) Tests

    [Fact]
    public async Task GenerateSummaryAsync_WithLanguage_FetchesBodyFromGraphQL()
    {
        // Arrange
        const int issueId = 123;
        const string language = "en";
        const string body = "Fetched body from GraphQL";
        var issue = CreateTestIssueWithRepository(issueId, "Olbrasoft/TestRepo");

        _mockQueryService.Setup(s => s.GetIssuesByIdsAsync(
                new[] { issueId },
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Issue> { issue });

        _mockGraphQLClient.Setup(c => c.FetchBodiesAsync(
                It.Is<IEnumerable<IssueBodyRequest>>(req =>
                    req.Any(r => r.Owner == "Olbrasoft" && r.Repo == "TestRepo" && r.Number == issueId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string, string, int), string>
            {
                { ("Olbrasoft", "TestRepo", issueId), body }
            });

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: "Summary",
                Provider: "AI",
                Error: null));

        // Act
        await _orchestrator.GenerateSummaryAsync(issueId, language);

        // Assert
        _mockGraphQLClient.Verify(c => c.FetchBodiesAsync(
            It.IsAny<IEnumerable<IssueBodyRequest>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockAiService.Verify(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithLanguage_WhenIssueNotFound_ReturnsEarly()
    {
        // Arrange
        const int issueId = 999;

        _mockQueryService.Setup(s => s.GetIssuesByIdsAsync(
                new[] { issueId },
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Issue>());

        // Act
        await _orchestrator.GenerateSummaryAsync(issueId, "en");

        // Assert
        _mockGraphQLClient.Verify(c => c.FetchBodiesAsync(
            It.IsAny<IEnumerable<IssueBodyRequest>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithLanguage_WhenNoBodyAvailable_ReturnsEarly()
    {
        // Arrange
        const int issueId = 123;
        var issue = CreateTestIssueWithRepository(issueId, "Olbrasoft/TestRepo");

        _mockQueryService.Setup(s => s.GetIssuesByIdsAsync(
                new[] { issueId },
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Issue> { issue });

        _mockGraphQLClient.Setup(c => c.FetchBodiesAsync(
                It.IsAny<IEnumerable<IssueBodyRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string, string, int), string>());

        // Act
        await _orchestrator.GenerateSummaryAsync(issueId, "en");

        // Assert
        _mockRepository.Verify(r => r.GetIssueByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GenerateSummaryAsync(int issueId) Tests

    [Fact]
    public async Task GenerateSummaryAsync_WithoutLanguage_DefaultsToBoth()
    {
        // Arrange
        const int issueId = 123;
        const string body = "Test body";
        var issue = CreateTestIssueWithRepository(issueId, "Olbrasoft/TestRepo");

        _mockQueryService.Setup(s => s.GetIssuesByIdsAsync(
                new[] { issueId },
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Issue> { issue });

        _mockGraphQLClient.Setup(c => c.FetchBodiesAsync(
                It.IsAny<IEnumerable<IssueBodyRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string, string, int), string>
            {
                { ("Olbrasoft", "TestRepo", issueId), body }
            });

        _mockRepository.Setup(r => r.GetIssueByIdAsync(issueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(body, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: "Summary",
                Provider: "AI",
                Error: null));

        _mockTranslationService.Setup(s => s.TranslateWithFallbackAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationFallbackResult(
                Success: true,
                Translation: "Překlad",
                Provider: "DeepL",
                UsedFallback: false,
                Error: null));

        // Act
        await _orchestrator.GenerateSummaryAsync(issueId);

        // Assert - Should generate both EN and CS
        _mockTranslationService.Verify(s => s.TranslateWithFallbackAsync(
            It.IsAny<string>(), "cs", "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GenerateSummariesAsync Tests

    [Fact]
    public async Task GenerateSummariesAsync_ProcessesAllIssues()
    {
        // Arrange
        var issuesWithBodies = new[]
        {
            (IssueId: 1, Body: "Body 1"),
            (IssueId: 2, Body: "Body 2"),
            (IssueId: 3, Body: "Body 3")
        };

        var issue = CreateTestIssue(1);
        _mockRepository.Setup(r => r.GetIssueByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: "Summary",
                Provider: "AI",
                Error: null));

        // Act
        await _orchestrator.GenerateSummariesAsync(issuesWithBodies, "en");

        // Assert
        _mockAiService.Verify(s => s.GenerateSummaryAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GenerateSummariesAsync_ContinuesOnError()
    {
        // Arrange
        var issuesWithBodies = new[]
        {
            (IssueId: 1, Body: "Body 1"),
            (IssueId: 2, Body: "Body 2")  // This will throw
        };

        var issue = CreateTestIssue(1);
        _mockRepository.Setup(r => r.GetIssueByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issue);
        _mockRepository.Setup(r => r.GetIssueByIdAsync(2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        _mockCacheService.Setup(s => s.GetIfFreshAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockAiService.Setup(s => s.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummarizationResult(
                Success: true,
                Summary: "Summary",
                Provider: "AI",
                Error: null));

        // Act - Should not throw
        await _orchestrator.GenerateSummariesAsync(issuesWithBodies, "en");

        // Assert - First issue should still be processed
        _mockAiService.Verify(s => s.GenerateSummaryAsync(
            "Body 1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private Issue CreateTestIssue(int id)
    {
        return new Issue
        {
            Id = id,
            Number = id,
            Title = $"Test Issue {id}",
            GitHubUpdatedAt = _testTime,
            RepositoryId = 1
        };
    }

    private Issue CreateTestIssueWithRepository(int number, string repositoryFullName)
    {
        return new Issue
        {
            Id = number,
            Number = number,
            Title = $"Test Issue {number}",
            GitHubUpdatedAt = _testTime,
            RepositoryId = 1,
            Repository = new Repository
            {
                Id = 1,
                GitHubId = 1000,
                FullName = repositoryFullName
            }
        };
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                null!,
                _mockCacheService.Object,
                _mockAiService.Object,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                null!,
                _mockAiService.Object,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullAiService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                null!,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullTranslationService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockAiService.Object,
                null!,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullNotificationService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockAiService.Object,
                _mockTranslationService.Object,
                null!,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullGraphQLClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockAiService.Object,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                null!,
                _mockQueryService.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullQueryService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockAiService.Object,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IssueSummaryOrchestrator(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockAiService.Object,
                _mockTranslationService.Object,
                _mockNotificationService.Object,
                _mockGraphQLClient.Object,
                _mockQueryService.Object,
                null!));
    }

    #endregion
}
