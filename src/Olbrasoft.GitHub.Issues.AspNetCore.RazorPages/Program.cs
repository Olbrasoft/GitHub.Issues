using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using System.Security.Claims;
using System.Text.Json;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;
using Olbrasoft.Mediation;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// Add session support for search state persistence
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".GitHubIssues.Session";
});

// Configure GitHub OAuth authentication
var gitHubClientId = builder.Configuration["GitHub:ClientId"];
var gitHubClientSecret = builder.Configuration["GitHub:ClientSecret"];
var gitHubOwner = builder.Configuration["GitHub:Owner"] ?? "Olbrasoft";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
})
.AddGitHub(options =>
{
    options.ClientId = gitHubClientId ?? throw new InvalidOperationException("GitHub:ClientId not configured");
    options.ClientSecret = gitHubClientSecret ?? throw new InvalidOperationException("GitHub:ClientSecret not configured. Add it to User Secrets.");
    options.Scope.Add("read:user");
    options.CallbackPath = "/signin-github";
    options.SaveTokens = true;
});

// Add authorization with owner policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy =>
        policy.RequireAssertion(context =>
        {
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value
                        ?? context.User.FindFirst("urn:github:login")?.Value;
            return string.Equals(username, gitHubOwner, StringComparison.OrdinalIgnoreCase);
        }));
});

// Configure database provider from settings
var databaseSettings = builder.Configuration.GetSection("Database").Get<DatabaseSettings>()
    ?? new DatabaseSettings { Provider = DatabaseProvider.PostgreSQL };

// Build connection string with secrets pattern
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dbPassword = builder.Configuration["DbPassword"];

if (!string.IsNullOrEmpty(dbPassword))
{
    connectionString += $";Password={dbPassword}";
}

// Register DbContext with multi-provider support
builder.Services.AddGitHubDbContext(connectionString!, databaseSettings.Provider);

// Configure settings
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<SearchSettings>(
    builder.Configuration.GetSection("Search"));
builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<BodyPreviewSettings>(
    builder.Configuration.GetSection("BodyPreview"));
builder.Services.Configure<AiProvidersSettings>(
    builder.Configuration.GetSection("AiProviders"));
builder.Services.Configure<SummarizationSettings>(
    builder.Configuration.GetSection("Summarization"));
builder.Services.Configure<TranslationSettings>(
    builder.Configuration.GetSection("Translation"));

// Get embedding settings to determine provider
var embeddingSettings = builder.Configuration.GetSection("Embeddings").Get<EmbeddingSettings>() ?? new EmbeddingSettings();

// Register Mediator and CQRS handlers
builder.Services.AddMediation(typeof(Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries.IssueSearchQuery).Assembly)
    .UseRequestHandlerMediator();

// Register embedding service based on provider configuration
if (embeddingSettings.Provider == EmbeddingProvider.Cohere)
{
    // Cohere cloud API - no local service management needed
    builder.Services.AddHttpClient<CohereEmbeddingService>();
    builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<CohereEmbeddingService>());
}
else
{
    // Ollama local service (default)
    builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
    builder.Services.AddSingleton<IServiceManager, SystemdServiceManager>();
    builder.Services.AddHttpClient<OllamaEmbeddingService>();
    builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
    builder.Services.AddScoped<IServiceLifecycleManager>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
}

// Register services
builder.Services.AddHttpClient<GitHubGraphQLClient>();
builder.Services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

// AI services - Singleton for rotation state persistence, but HttpClient via factory
builder.Services.AddHttpClient<IAiSummarizationService, AiSummarizationService>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<IAiTranslationService, AiTranslationService>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddScoped<IIssueSearchService, IssueSearchService>();
builder.Services.AddScoped<IIssueDetailService, IssueDetailService>();
builder.Services.AddScoped<IDatabaseStatusService, DatabaseStatusService>();

// SignalR notifiers for progressive updates
builder.Services.AddScoped<ISummaryNotifier, SignalRSummaryNotifier>();
builder.Services.AddScoped<ITranslationNotifier, SignalRTranslationNotifier>();

// Title translation service for search results
builder.Services.AddScoped<ITitleTranslationService, TitleTranslationService>();

// Register Sync services for data import
builder.Services.Configure<SyncSettings>(builder.Configuration.GetSection("Sync"));
builder.Services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();
builder.Services.AddScoped<IIssueSyncBusinessService, IssueSyncBusinessService>();
builder.Services.AddScoped<ILabelSyncBusinessService, LabelSyncBusinessService>();
builder.Services.AddScoped<IRepositorySyncBusinessService, RepositorySyncBusinessService>();
builder.Services.AddScoped<IEventSyncBusinessService, EventSyncBusinessService>();
// Repository sync - refactored with SRP
builder.Services.AddHttpClient<IGitHubRepositoryApiClient, GitHubRepositoryApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
}).ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});
builder.Services.AddScoped<IRepositorySyncService, RepositorySyncService>();
builder.Services.AddScoped<ILabelSyncService, LabelSyncService>();
// Issue sync - refactored with SRP
builder.Services.AddHttpClient<IGitHubIssueApiClient, GitHubIssueApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
}).ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});
builder.Services.AddSingleton<IEmbeddingTextBuilder>(sp =>
{
    var syncSettings = sp.GetRequiredService<IOptions<SyncSettings>>();
    return new EmbeddingTextBuilder(syncSettings.Value.MaxEmbeddingTextLength);
});
builder.Services.AddScoped<IIssueSyncService, IssueSyncService>();
// Event sync - refactored with SRP (shares GitHub API config with issue client)
builder.Services.AddHttpClient<IGitHubEventApiClient, GitHubEventApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
}).ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});
builder.Services.AddScoped<IEventSyncService, EventSyncService>();
builder.Services.AddScoped<IGitHubSyncService, GitHubSyncService>();

// Webhook services for real-time sync
builder.Services.Configure<WebhookSettings>(builder.Configuration.GetSection("GitHubApp"));
builder.Services.AddSingleton<IWebhookSignatureValidator, WebhookSignatureValidator>();
builder.Services.AddScoped<IIssueUpdateNotifier, SignalRIssueUpdateNotifier>();
builder.Services.AddScoped<IGitHubWebhookService, GitHubWebhookService>();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<IssueUpdatesHub>("/hubs/issues");

// Authentication endpoints
app.MapGet("/login", async (HttpContext context) =>
{
    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    await context.ChallengeAsync("GitHub", new AuthenticationProperties
    {
        RedirectUri = returnUrl
    });
});

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

// Auth status endpoint for JavaScript
app.MapGet("/api/auth/status", (HttpContext context, IConfiguration config) =>
{
    var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
    var username = context.User.FindFirst(ClaimTypes.Name)?.Value
                ?? context.User.FindFirst("urn:github:login")?.Value;
    var owner = config["GitHub:Owner"] ?? "Olbrasoft";
    var isOwner = isAuthenticated && string.Equals(username, owner, StringComparison.OrdinalIgnoreCase);

    return Results.Ok(new
    {
        isAuthenticated,
        username,
        isOwner
    });
});

// API endpoints
app.MapGet("/api/repositories/search", async (string? term, IMediator mediator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(term))
    {
        return Results.Ok(Array.Empty<object>());
    }

    var query = new RepositoriesSearchQuery(mediator)
    {
        Term = term,
        MaxResults = 15
    };

    var results = await query.ToResultAsync(ct);
    return Results.Ok(results);
});

// Database status and management endpoints
app.MapGet("/api/database/status", async (IDatabaseStatusService dbStatus, CancellationToken ct) =>
{
    var status = await dbStatus.GetStatusAsync(ct);
    return Results.Ok(status);
});

app.MapPost("/api/database/migrate", async (IDatabaseStatusService dbStatus, CancellationToken ct) =>
{
    var result = await dbStatus.ApplyMigrationsAsync(ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization("OwnerOnly");

app.MapPost("/api/data/import", async (
    IGitHubSyncService syncService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("Starting data import from GitHub (smart sync mode)");
        await syncService.SyncAllRepositoriesAsync(since: null, smartMode: true, ct);

        return Results.Ok(new { success = true, message = "Import dokončen" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Import failed");
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
}).RequireAuthorization("OwnerOnly");

// Get all repositories with sync status
app.MapGet("/api/repositories/sync-status", async (IMediator mediator, CancellationToken ct) =>
{
    var query = new RepositoriesSyncStatusQuery(mediator);
    var results = await query.ToResultAsync(ct);
    return Results.Ok(results);
});

// Generate AI summary for issue (progressive loading via SignalR)
app.MapPost("/api/issues/{id:int}/generate-summary", async (
    int id,
    IIssueDetailService issueDetailService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Fire and forget - run summary generation in background
    // The result will be pushed via SignalR
    _ = Task.Run(async () =>
    {
        try
        {
            await issueDetailService.GenerateSummaryAsync(id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background summary generation failed for issue {Id}", id);
        }
    });

    return Results.Accepted();
});

// Translate issue titles to Czech (progressive loading via SignalR)
app.MapPost("/api/issues/translate-titles", async (
    HttpRequest request,
    ITitleTranslationService translationService,
    ITranslationNotifier notifier,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Parse issue IDs from request body
    List<int> issueIds;
    try
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var json = JsonDocument.Parse(body);

        if (!json.RootElement.TryGetProperty("issueIds", out var idsElement) ||
            idsElement.ValueKind != JsonValueKind.Array)
        {
            return Results.BadRequest(new { error = "Missing issueIds array" });
        }

        issueIds = idsElement.EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Invalid JSON" });
    }

    if (issueIds.Count == 0)
    {
        return Results.Ok(new { translated = 0 });
    }

    // Fire and forget - translate in background and push via SignalR
    _ = Task.Run(async () =>
    {
        try
        {
            var results = await translationService.TranslateTitlesAsync(issueIds, CancellationToken.None);

            foreach (var result in results.Where(r => r.Success && r.CzechTitle != null))
            {
                await notifier.NotifyTitleTranslationAsync(
                    new TitleTranslationNotificationDto(result.IssueId, result.CzechTitle!),
                    CancellationToken.None);
            }

            logger.LogInformation(
                "Translated {Count}/{Total} titles",
                results.Count(r => r.Success),
                issueIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background title translation failed");
        }
    });

    return Results.Accepted(value: new { queued = issueIds.Count });
});

// Sync specific repository or all repositories
app.MapPost("/api/data/sync", async (
    HttpRequest request,
    IGitHubSyncService syncService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        // Parse request body
        List<string>? repositoryFullNames = null;
        bool fullRefresh = false;

        if (request.ContentLength > 0)
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var json = JsonDocument.Parse(body);

                // Support both single repo (legacy) and multiple repos
                if (json.RootElement.TryGetProperty("repositoryFullNames", out var reposElement) && reposElement.ValueKind == JsonValueKind.Array)
                {
                    repositoryFullNames = reposElement.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Cast<string>()
                        .ToList();
                }
                else if (json.RootElement.TryGetProperty("repositoryFullName", out var repoElement))
                {
                    var singleRepo = repoElement.GetString();
                    if (!string.IsNullOrWhiteSpace(singleRepo))
                    {
                        repositoryFullNames = new List<string> { singleRepo };
                    }
                }

                if (json.RootElement.TryGetProperty("fullRefresh", out var refreshElement))
                {
                    fullRefresh = refreshElement.GetBoolean();
                }
            }
        }

        // Determine sync mode
        var smartMode = !fullRefresh;

        Olbrasoft.GitHub.Issues.Data.Dtos.SyncStatisticsDto stats;

        if (repositoryFullNames != null && repositoryFullNames.Count > 0)
        {
            // Validate all repository formats first
            foreach (var repoFullName in repositoryFullNames)
            {
                var parts = repoFullName.Split('/');
                if (parts.Length != 2)
                {
                    return Results.BadRequest(new { success = false, message = $"Invalid repository format: {repoFullName}. Expected: owner/repo" });
                }
            }

            logger.LogInformation("Starting sync for {Count} repositories (smartMode: {SmartMode})", repositoryFullNames.Count, smartMode);
            stats = await syncService.SyncRepositoriesAsync(repositoryFullNames, since: null, smartMode: smartMode, ct);

            var repoLabel = repositoryFullNames.Count == 1 ? repositoryFullNames[0] : $"{repositoryFullNames.Count} repozitářů";
            return Results.Ok(new
            {
                success = true,
                message = $"Synchronizace {repoLabel} dokončena",
                statistics = new
                {
                    apiCalls = stats.ApiCalls,
                    totalFound = stats.TotalFound,
                    created = stats.Created,
                    updated = stats.Updated,
                    unchanged = stats.Unchanged,
                    sinceTimestamp = stats.SinceTimestamp
                }
            });
        }
        else
        {
            // Sync all repositories
            logger.LogInformation("Starting sync for all repositories (smartMode: {SmartMode})", smartMode);
            stats = await syncService.SyncAllRepositoriesAsync(since: null, smartMode: smartMode, ct);

            return Results.Ok(new
            {
                success = true,
                message = "Synchronizace všech repozitářů dokončena",
                statistics = new
                {
                    apiCalls = stats.ApiCalls,
                    totalFound = stats.TotalFound,
                    created = stats.Created,
                    updated = stats.Updated,
                    unchanged = stats.Unchanged,
                    sinceTimestamp = stats.SinceTimestamp
                }
            });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Sync failed");
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
}).RequireAuthorization("OwnerOnly");

// GitHub Webhook endpoint for real-time sync
// Handles: issues, issue_comment, repository, label events
app.MapPost("/api/webhooks/github", async (
    HttpRequest request,
    IWebhookSignatureValidator signatureValidator,
    IGitHubWebhookService webhookService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Read raw body for signature validation
    request.EnableBuffering();
    using var memoryStream = new MemoryStream();
    await request.Body.CopyToAsync(memoryStream, ct);
    var payload = memoryStream.ToArray();
    request.Body.Position = 0;

    // Validate signature
    var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
    if (!signatureValidator.ValidateSignature(payload, signature))
    {
        logger.LogWarning("Invalid webhook signature");
        return Results.Unauthorized();
    }

    // Check event type
    var eventType = request.Headers["X-GitHub-Event"].FirstOrDefault();
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    try
    {
        WebhookProcessingResult result;

        switch (eventType)
        {
            case "issues":
                request.Body.Position = 0;
                var issuePayload = await JsonSerializer.DeserializeAsync<GitHubIssueWebhookPayload>(
                    request.Body, jsonOptions, ct);
                if (issuePayload == null)
                    return Results.BadRequest(new { error = "Empty payload" });
                result = await webhookService.ProcessIssueEventAsync(issuePayload, ct);
                break;

            case "issue_comment":
                request.Body.Position = 0;
                var commentPayload = await JsonSerializer.DeserializeAsync<GitHubIssueCommentWebhookPayload>(
                    request.Body, jsonOptions, ct);
                if (commentPayload == null)
                    return Results.BadRequest(new { error = "Empty payload" });
                result = await webhookService.ProcessIssueCommentEventAsync(commentPayload, ct);
                break;

            case "repository":
                request.Body.Position = 0;
                var repoPayload = await JsonSerializer.DeserializeAsync<GitHubRepositoryWebhookPayload>(
                    request.Body, jsonOptions, ct);
                if (repoPayload == null)
                    return Results.BadRequest(new { error = "Empty payload" });
                result = await webhookService.ProcessRepositoryEventAsync(repoPayload, ct);
                break;

            case "label":
                request.Body.Position = 0;
                var labelPayload = await JsonSerializer.DeserializeAsync<GitHubLabelWebhookPayload>(
                    request.Body, jsonOptions, ct);
                if (labelPayload == null)
                    return Results.BadRequest(new { error = "Empty payload" });
                result = await webhookService.ProcessLabelEventAsync(labelPayload, ct);
                break;

            default:
                logger.LogDebug("Ignoring webhook event type: {EventType}", eventType);
                return Results.Ok(new { message = $"Ignored event: {eventType}" });
        }

        if (result.Success)
        {
            logger.LogInformation("Webhook {Event} processed: {Message}", eventType, result.Message);
            return Results.Ok(result);
        }
        else
        {
            logger.LogWarning("Webhook {Event} failed: {Message}", eventType, result.Message);
            return Results.BadRequest(result);
        }
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "Failed to parse webhook payload for event: {EventType}", eventType);
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }
});

app.Run();
