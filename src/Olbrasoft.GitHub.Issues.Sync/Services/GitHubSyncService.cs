using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

public class GitHubSyncService : IGitHubSyncService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly GitHubClient _gitHubClient;
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        GitHubDbContext dbContext,
        IEmbeddingService embeddingService,
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _gitHubClient = new GitHubClient(new Octokit.ProductHeaderValue("Olbrasoft-GitHub-Issues-Sync"));

        // Configure HttpClient for GitHub API
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _gitHubClient.Credentials = new Credentials(_settings.Token);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }

    public async Task SyncAllRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<string> repositories;

        if (_settings.Repositories.Count > 0)
        {
            // Use explicit list from config
            _logger.LogInformation("Using explicit repository list from configuration ({Count} repositories)", _settings.Repositories.Count);
            repositories = _settings.Repositories;
        }
        else if (!string.IsNullOrEmpty(_settings.Owner))
        {
            // Discover repos via API
            _logger.LogInformation("Discovering repositories for owner: {Owner} (type: {OwnerType})", _settings.Owner, _settings.OwnerType);
            repositories = await FetchAllRepositoriesForOwnerAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("No repositories to sync. Configure either 'Repositories' list or 'Owner' in settings.");
            return;
        }

        await SyncRepositoriesAsync(repositories, cancellationToken);
    }

    public async Task SyncRepositoriesAsync(IEnumerable<string> repositories, CancellationToken cancellationToken = default)
    {
        foreach (var repoFullName in repositories)
        {
            var parts = repoFullName.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid repository format: {Repository}. Expected 'owner/repo'", repoFullName);
                continue;
            }

            await SyncRepositoryAsync(parts[0], parts[1], cancellationToken);
        }
    }

    /// <summary>
    /// Fetches all repositories for the configured owner using GitHub API.
    /// Filters based on IncludeArchived, IncludeForks, and has_issues settings.
    /// </summary>
    private async Task<List<string>> FetchAllRepositoriesForOwnerAsync(CancellationToken cancellationToken)
    {
        var repositories = new List<string>();
        var page = 1;
        var owner = _settings.Owner!;

        while (true)
        {
            // Use different API endpoint for users vs organizations
            var url = _settings.OwnerType.Equals("org", StringComparison.OrdinalIgnoreCase)
                ? $"orgs/{owner}/repos?per_page=100&page={page}"
                : $"users/{owner}/repos?per_page=100&type=all&page={page}";

            _logger.LogDebug("Fetching repositories page {Page} for {Owner}", page, owner);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageRepos = doc.RootElement.EnumerateArray().ToList();
            if (pageRepos.Count == 0)
            {
                break;
            }

            foreach (var repo in pageRepos)
            {
                var fullName = repo.GetProperty("full_name").GetString();
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                // Check has_issues - skip repos with issues disabled
                if (repo.TryGetProperty("has_issues", out var hasIssuesElement) && !hasIssuesElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: has_issues=false", fullName);
                    continue;
                }

                // Check archived - skip unless IncludeArchived is true
                if (!_settings.IncludeArchived &&
                    repo.TryGetProperty("archived", out var archivedElement) && archivedElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: archived", fullName);
                    continue;
                }

                // Check fork - skip unless IncludeForks is true
                if (!_settings.IncludeForks &&
                    repo.TryGetProperty("fork", out var forkElement) && forkElement.GetBoolean())
                {
                    _logger.LogDebug("Skipping {Repo}: fork", fullName);
                    continue;
                }

                repositories.Add(fullName);
            }

            if (pageRepos.Count < 100)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Discovered {Count} repositories for {Owner}", repositories.Count, owner);
        return repositories;
    }

    public async Task SyncRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync for {Owner}/{Repo}", owner, repo);

        var repository = await EnsureRepositoryAsync(owner, repo, cancellationToken);
        await SyncLabelsAsync(repository, owner, repo, cancellationToken);
        await SyncIssuesAsync(repository, owner, repo, cancellationToken); // Also handles parent-child relationships via parent_issue_url
        await SyncEventsAsync(repository, owner, repo, cancellationToken);

        _logger.LogInformation("Completed sync for {Owner}/{Repo}", owner, repo);
    }

    private async Task<Data.Entities.Repository> EnsureRepositoryAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var fullName = $"{owner}/{repo}";
        var repository = await _dbContext.Repositories
            .FirstOrDefaultAsync(r => r.FullName == fullName, cancellationToken);

        if (repository == null)
        {
            var ghRepo = await _gitHubClient.Repository.Get(owner, repo);

            repository = new Data.Entities.Repository
            {
                GitHubId = ghRepo.Id,
                FullName = ghRepo.FullName,
                HtmlUrl = ghRepo.HtmlUrl
            };

            _dbContext.Repositories.Add(repository);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created repository: {FullName}", fullName);
        }

        return repository;
    }

    private async Task SyncIssuesAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing issues for {Owner}/{Repo} using bulk API", owner, repo);

        var syncedAt = DateTimeOffset.UtcNow;
        var allIssues = new List<JsonElement>();
        var page = 1;

        // Fetch all issues using bulk API with pagination
        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues?state=all&per_page=100&page={page}";
            _logger.LogDebug("Fetching issues page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageIssues = doc.RootElement.EnumerateArray().ToList();
            if (pageIssues.Count == 0)
            {
                break;
            }

            // Clone elements since JsonDocument will be disposed
            foreach (var issue in pageIssues)
            {
                allIssues.Add(issue.Clone());
            }

            _logger.LogDebug("Fetched {Count} issues on page {Page}", pageIssues.Count, page);

            if (pageIssues.Count < 100)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} issues to sync for {Owner}/{Repo}", allIssues.Count, owner, repo);

        // Dictionary to track parent_issue_url -> child issue number for later FK update
        var parentChildRelationships = new Dictionary<int, string>(); // childNumber -> parentIssueUrl

        foreach (var ghIssue in allIssues)
        {
            // Skip pull requests (they have pull_request property)
            if (ghIssue.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            var issueNumber = ghIssue.GetProperty("number").GetInt32();
            var title = ghIssue.GetProperty("title").GetString() ?? "";
            var state = ghIssue.GetProperty("state").GetString();
            var htmlUrl = ghIssue.GetProperty("html_url").GetString() ?? "";
            var updatedAt = ghIssue.GetProperty("updated_at").GetDateTimeOffset();
            var createdAt = ghIssue.GetProperty("created_at").GetDateTimeOffset();

            // Extract parent_issue_url if present
            if (ghIssue.TryGetProperty("parent_issue_url", out var parentUrlElement) &&
                parentUrlElement.ValueKind == JsonValueKind.String)
            {
                var parentUrl = parentUrlElement.GetString();
                if (!string.IsNullOrEmpty(parentUrl))
                {
                    parentChildRelationships[issueNumber] = parentUrl;
                }
            }

            // Get or create issue
            var issue = await _dbContext.Issues
                .Include(i => i.IssueLabels)
                .FirstOrDefaultAsync(i => i.RepositoryId == repository.Id && i.Number == issueNumber, cancellationToken);

            var isNew = issue == null;

            if (isNew)
            {
                issue = new Data.Entities.Issue
                {
                    RepositoryId = repository.Id,
                    Number = issueNumber
                };
                _dbContext.Issues.Add(issue);
            }

            issue!.Title = title;
            issue.IsOpen = state == "open";
            issue.Url = htmlUrl;
            issue.GitHubUpdatedAt = updatedAt;
            issue.SyncedAt = syncedAt;

            // Generate embedding for new issues
            if (isNew)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(title, cancellationToken);
                if (embedding == null)
                {
                    throw new InvalidOperationException($"Failed to generate embedding for issue #{issueNumber}. Ollama may be unavailable.");
                }
                issue.TitleEmbedding = embedding;
            }

            // Sync labels
            if (ghIssue.TryGetProperty("labels", out var labelsElement))
            {
                var existingLabelIds = issue.IssueLabels.Select(il => il.LabelId).ToHashSet();
                var ghLabelNames = new List<string>();

                foreach (var labelElement in labelsElement.EnumerateArray())
                {
                    if (labelElement.TryGetProperty("name", out var nameElement))
                    {
                        ghLabelNames.Add(nameElement.GetString() ?? "");
                    }
                }

                // Remove labels that are no longer present
                var labelsToRemove = issue.IssueLabels
                    .Where(il => !ghLabelNames.Contains(_dbContext.Labels.Find(il.LabelId)?.Name ?? ""))
                    .ToList();

                foreach (var labelToRemove in labelsToRemove)
                {
                    issue.IssueLabels.Remove(labelToRemove);
                }

                // Add new labels
                foreach (var labelName in ghLabelNames)
                {
                    var label = await _dbContext.Labels.FirstOrDefaultAsync(
                        l => l.RepositoryId == repository.Id && l.Name == labelName, cancellationToken);
                    if (label != null && !existingLabelIds.Contains(label.Id))
                    {
                        issue.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = label.Id });
                    }
                }
            }

            _logger.LogDebug("Synced issue #{Number}: {Title}", issueNumber, title);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update parent-child relationships after all issues are synced
        if (parentChildRelationships.Count > 0)
        {
            _logger.LogInformation("Updating {Count} parent-child relationships", parentChildRelationships.Count);

            var issuesByNumber = await _dbContext.Issues
                .Where(i => i.RepositoryId == repository.Id)
                .ToDictionaryAsync(i => i.Number, cancellationToken);

            var updatedCount = 0;
            foreach (var (childNumber, parentUrl) in parentChildRelationships)
            {
                // Parse parent issue number from URL (e.g., https://api.github.com/repos/owner/repo/issues/123)
                var parentNumber = ExtractIssueNumberFromUrl(parentUrl);
                if (parentNumber.HasValue && issuesByNumber.TryGetValue(parentNumber.Value, out var parentIssue))
                {
                    if (issuesByNumber.TryGetValue(childNumber, out var childIssue))
                    {
                        if (childIssue.ParentIssueId != parentIssue.Id)
                        {
                            childIssue.ParentIssueId = parentIssue.Id;
                            updatedCount++;
                            _logger.LogDebug("Set parent of issue #{Child} to #{Parent}", childNumber, parentNumber.Value);
                        }
                    }
                }
            }

            if (updatedCount > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated {Count} parent-child relationships", updatedCount);
            }
        }
    }

    /// <summary>
    /// Extracts issue number from GitHub API URL (e.g., https://api.github.com/repos/owner/repo/issues/123)
    /// </summary>
    private static int? ExtractIssueNumberFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // URL format: https://api.github.com/repos/{owner}/{repo}/issues/{number}
        var lastSlashIndex = url.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < url.Length - 1)
        {
            var numberPart = url[(lastSlashIndex + 1)..];
            if (int.TryParse(numberPart, out var number))
            {
                return number;
            }
        }

        return null;
    }

    private async Task SyncLabelsAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var ghLabels = await _gitHubClient.Issue.Labels.GetAllForRepository(owner, repo);

        foreach (var ghLabel in ghLabels)
        {
            var label = await _dbContext.Labels
                .FirstOrDefaultAsync(l => l.RepositoryId == repository.Id && l.Name == ghLabel.Name, cancellationToken);

            if (label == null)
            {
                label = new Data.Entities.Label { RepositoryId = repository.Id, Name = ghLabel.Name, Color = ghLabel.Color };
                _dbContext.Labels.Add(label);
                _logger.LogDebug("Created label: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
            else if (label.Color != ghLabel.Color)
            {
                label.Color = ghLabel.Color;
                _logger.LogDebug("Updated label color: {Name} ({Color})", ghLabel.Name, ghLabel.Color);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncEventsAsync(
        Data.Entities.Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing issue events for {Owner}/{Repo} using bulk API", owner, repo);

        // Cache event types for faster lookup
        var eventTypes = await _dbContext.EventTypes.ToDictionaryAsync(et => et.Name, cancellationToken);

        // Get all issues for this repository (to map issue numbers to IDs)
        var issuesByNumber = await _dbContext.Issues
            .Where(i => i.RepositoryId == repository.Id)
            .ToDictionaryAsync(i => i.Number, cancellationToken);

        // Get existing event IDs to avoid duplicates
        var existingEventIds = await _dbContext.IssueEvents
            .Where(ie => issuesByNumber.Values.Select(i => i.Id).Contains(ie.IssueId))
            .Select(ie => ie.GitHubEventId)
            .ToHashSetAsync(cancellationToken);

        var allEvents = new List<JsonElement>();
        var page = 1;

        // Fetch all events using bulk repo events endpoint with pagination
        while (true)
        {
            var url = $"repos/{owner}/{repo}/issues/events?per_page=100&page={page}";
            _logger.LogDebug("Fetching events page {Page}", page);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var pageEvents = doc.RootElement.EnumerateArray().ToList();
            if (pageEvents.Count == 0)
            {
                break;
            }

            // Clone elements since JsonDocument will be disposed
            foreach (var evt in pageEvents)
            {
                allEvents.Add(evt.Clone());
            }

            _logger.LogDebug("Fetched {Count} events on page {Page}", pageEvents.Count, page);

            if (pageEvents.Count < 100)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Found {Count} events to process for {Owner}/{Repo}", allEvents.Count, owner, repo);

        var newEventsCount = 0;
        var skippedCount = 0;

        foreach (var ghEvent in allEvents)
        {
            // Get GitHub event ID
            var eventId = ghEvent.GetProperty("id").GetInt64();

            // Skip if already synced
            if (existingEventIds.Contains(eventId))
            {
                continue;
            }

            // Get issue number from the issue object in the event
            if (!ghEvent.TryGetProperty("issue", out var issueElement))
            {
                skippedCount++;
                continue;
            }

            var issueNumber = issueElement.GetProperty("number").GetInt32();

            // Find the issue in our database
            if (!issuesByNumber.TryGetValue(issueNumber, out var issue))
            {
                skippedCount++;
                continue;
            }

            // Get event type
            var eventTypeName = ghEvent.GetProperty("event").GetString() ?? "";

            if (!eventTypes.TryGetValue(eventTypeName, out var eventType))
            {
                _logger.LogDebug("Unknown event type: {EventType}, skipping", eventTypeName);
                skippedCount++;
                continue;
            }

            // Get actor info
            int? actorId = null;
            string? actorLogin = null;
            if (ghEvent.TryGetProperty("actor", out var actorElement) && actorElement.ValueKind != JsonValueKind.Null)
            {
                if (actorElement.TryGetProperty("id", out var actorIdElement))
                {
                    actorId = actorIdElement.GetInt32();
                }
                if (actorElement.TryGetProperty("login", out var actorLoginElement))
                {
                    actorLogin = actorLoginElement.GetString();
                }
            }

            // Get created_at
            var createdAt = ghEvent.GetProperty("created_at").GetDateTimeOffset();

            var issueEvent = new Data.Entities.IssueEvent
            {
                IssueId = issue.Id,
                EventTypeId = eventType.Id,
                GitHubEventId = eventId,
                CreatedAt = createdAt,
                ActorId = actorId,
                ActorLogin = actorLogin
            };

            _dbContext.IssueEvents.Add(issueEvent);
            existingEventIds.Add(eventId); // Track to avoid duplicates within same sync
            newEventsCount++;

            // Periodic save every 100 events
            if (newEventsCount % 100 == 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Saved {Count} events so far...", newEventsCount);
            }
        }

        if (newEventsCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Synced {Count} new issue events (skipped {Skipped})", newEventsCount, skippedCount);
        }
        else
        {
            _logger.LogInformation("No new issue events to sync (skipped {Skipped})", skippedCount);
        }
    }
}
