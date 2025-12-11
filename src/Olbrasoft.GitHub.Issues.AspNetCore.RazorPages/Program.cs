using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using System.Text.Json;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Mediation;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

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
builder.Services.AddHttpClient<AiSummarizationService>();
builder.Services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());
// Singleton - rotation state must persist across requests
builder.Services.AddSingleton<IAiSummarizationService>(sp => sp.GetRequiredService<AiSummarizationService>());
builder.Services.AddScoped<IIssueSearchService, IssueSearchService>();
builder.Services.AddScoped<IIssueDetailService, IssueDetailService>();
builder.Services.AddScoped<IDatabaseStatusService, DatabaseStatusService>();

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

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

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
});

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
});

// Get all repositories with sync status
app.MapGet("/api/repositories/sync-status", async (IMediator mediator, CancellationToken ct) =>
{
    var query = new RepositoriesSyncStatusQuery(mediator);
    var results = await query.ToResultAsync(ct);
    return Results.Ok(results);
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
        string? repositoryFullName = null;
        bool fullRefresh = false;

        if (request.ContentLength > 0)
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("repositoryFullName", out var repoElement))
                {
                    repositoryFullName = repoElement.GetString();
                }
                if (json.RootElement.TryGetProperty("fullRefresh", out var refreshElement))
                {
                    fullRefresh = refreshElement.GetBoolean();
                }
            }
        }

        // Determine sync mode
        var smartMode = !fullRefresh;

        if (!string.IsNullOrWhiteSpace(repositoryFullName))
        {
            // Sync specific repository
            var parts = repositoryFullName.Split('/');
            if (parts.Length != 2)
            {
                return Results.BadRequest(new { success = false, message = "Invalid repository format. Expected: owner/repo" });
            }

            logger.LogInformation("Starting sync for {Repository} (smartMode: {SmartMode})", repositoryFullName, smartMode);
            await syncService.SyncRepositoryAsync(parts[0], parts[1], since: null, smartMode: smartMode, ct);

            return Results.Ok(new { success = true, message = $"Synchronizace {repositoryFullName} dokončena" });
        }
        else
        {
            // Sync all repositories
            logger.LogInformation("Starting sync for all repositories (smartMode: {SmartMode})", smartMode);
            await syncService.SyncAllRepositoriesAsync(since: null, smartMode: smartMode, ct);

            return Results.Ok(new { success = true, message = "Synchronizace všech repozitářů dokončena" });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Sync failed");
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();
