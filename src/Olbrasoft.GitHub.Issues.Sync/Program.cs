using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Database;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Configuration;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;
using Olbrasoft.Mediation;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

// Add SecureStore for encrypted secrets (same vault as web app)
var secureStoreConfig = builder.Configuration.GetSection("SecureStore");
var secretsPath = secureStoreConfig["SecretsPath"] ?? "~/.config/github-issues/secrets/secrets.json";
var keyPath = secureStoreConfig["KeyPath"] ?? "~/.config/github-issues/keys/secrets.key";
builder.Configuration.AddSecureStore(secretsPath, keyPath);

// Configure DbContext for SQL Server (NOT PostgreSQL!)
// Database credentials loaded from SecureStore (DbUserId, DbPassword)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var dbUserId = builder.Configuration["DbUserId"];
var dbPassword = builder.Configuration["DbPassword"];
if (!string.IsNullOrEmpty(dbUserId))
{
    connectionString += $";User Id={dbUserId}";
}
if (!string.IsNullOrEmpty(dbPassword))
{
    connectionString += $";Password={dbPassword}";
}
builder.Services.AddGitHubDbContext(connectionString, DatabaseProvider.SqlServer);

// Register Mediator
builder.Services.AddMediation(typeof(GitHubDbContext).Assembly).UseRequestHandlerMediator();

// Configure embedding service (Cohere only)
var textTransformSection = builder.Configuration.GetSection("TextTransformation");
var embeddingSection = textTransformSection.Exists()
    ? textTransformSection.GetSection("Embeddings")
    : builder.Configuration.GetSection("Embeddings");
builder.Services.Configure<EmbeddingSettings>(embeddingSection);

// PostConfigure to collect Cohere API keys from various locations (same as web app)
builder.Services.PostConfigure<EmbeddingSettings>(options =>
{
    var keys = new List<string>();

    // 1. TextTransformation:Embeddings:Cohere:ApiKeys (array)
    var cohereSection = builder.Configuration.GetSection("TextTransformation:Embeddings:Cohere:ApiKeys");
    var arrayKeys = cohereSection.Get<string[]>() ?? [];
    keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));

    // 2. AiProviders:Cohere:Keys (array)
    if (keys.Count == 0)
    {
        var aiProviderKeys = builder.Configuration.GetSection("AiProviders:Cohere:Keys").Get<string[]>() ?? [];
        keys.AddRange(aiProviderKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
    }

    // 3. Individual keys from SecureStore (AiProviders:Cohere:Key1, Key2, etc.)
    if (keys.Count == 0)
    {
        keys.AddRange(ConfigurationKeyLoader.LoadNumberedKeys(builder.Configuration, "AiProviders:Cohere:Key"));
    }

    // 4. Single key fallbacks (for user-secrets)
    if (keys.Count == 0)
    {
        var singleKey = builder.Configuration["TextTransformation:Embeddings:Cohere:ApiKey"]
                     ?? builder.Configuration["Embeddings:CohereApiKey"]
                     ?? builder.Configuration["CohereApiKey"]
                     ?? builder.Configuration["Cohere__ApiKey"];
        if (!string.IsNullOrWhiteSpace(singleKey))
        {
            keys.Add(singleKey);
        }
    }

    if (keys.Count > 0)
    {
        options.Cohere.ApiKeys = keys.ToArray();
    }
});

builder.Services.AddHttpClient<CohereEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<CohereEmbeddingService>());

// Embedding text builder
builder.Services.AddSingleton<IEmbeddingTextBuilder>(sp =>
{
    var syncSettings = sp.GetRequiredService<IOptions<SyncSettings>>();
    return new EmbeddingTextBuilder(syncSettings.Value.MaxEmbeddingTextLength);
});

// Issue embedding generator
builder.Services.AddScoped<IIssueEmbeddingGenerator, IssueEmbeddingGenerator>();

// Configure settings
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<SyncSettings>(builder.Configuration.GetSection("Sync"));

// Register Business layer sync services (Clean Architecture)
builder.Services.AddScoped<IIssueSyncBusinessService, IssueSyncBusinessService>();
builder.Services.AddScoped<ILabelSyncBusinessService, LabelSyncBusinessService>();
builder.Services.AddScoped<IRepositorySyncBusinessService, RepositorySyncBusinessService>();
builder.Services.AddScoped<IEventSyncBusinessService, EventSyncBusinessService>();

// Register GitHub API clients (typed HttpClients - these have HttpClient in constructor)
builder.Services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();

// Configure HTTP clients for GitHub API with base address and authorization
builder.Services.AddHttpClient<IGitHubRepositoryApiClient, GitHubRepositoryApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
})
.ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});

builder.Services.AddHttpClient<IGitHubIssueApiClient, GitHubIssueApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
})
.ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});

builder.Services.AddHttpClient<IGitHubEventApiClient, GitHubEventApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
})
.ConfigureHttpClient((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
    if (!string.IsNullOrEmpty(settings.Value.Token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.Value.Token);
    }
});

// Register specialized sync services (SRP - each has one responsibility)
// These do NOT have HttpClient in constructor - they depend on API client interfaces
builder.Services.AddScoped<IRepositorySyncService, RepositorySyncService>();
builder.Services.AddScoped<ILabelSyncService, LabelSyncService>();
builder.Services.AddScoped<IIssueSyncService, IssueSyncService>();
builder.Services.AddScoped<IEventSyncService, EventSyncService>();

// Register orchestrator (coordinates all sync services)
builder.Services.AddScoped<IGitHubSyncService, GitHubSyncService>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();

// Parse command line arguments
if (args.Length == 0 || args[0].ToLowerInvariant() != "sync")
{
    logger.LogInformation("Usage:");
    logger.LogInformation("  sync                                        - Full sync of all repositories");
    logger.LogInformation("  sync --smart                                - Smart sync (auto-use last_synced_at from DB)");
    logger.LogInformation("  sync --repo Owner/Repo                      - Full sync of specific repository");
    logger.LogInformation("  sync --repo X --repo Y                      - Full sync of multiple repositories");
    logger.LogInformation("  sync --since 2025-12-10T00:00:00Z           - Incremental sync (changes since timestamp)");
    logger.LogInformation("  sync --since 2025-12-10T00:00:00Z --repo X  - Incremental sync of specific repo");
    logger.LogInformation("  sync --smart --repo X                       - Smart sync of specific repo");
    logger.LogInformation("");
    logger.LogInformation("Options:");
    logger.LogInformation("  --repo Owner/Repo     Target specific repository (can be repeated)");
    logger.LogInformation("  --since TIMESTAMP     Incremental sync: only issues changed since TIMESTAMP (ISO 8601)");
    logger.LogInformation("  --smart               Use stored last_synced_at timestamp (auto-incremental)");
    return;
}

// Parse flags
var targetRepos = new List<string>();
DateTimeOffset? sinceTimestamp = null;
var smartMode = args.Contains("--smart", StringComparer.OrdinalIgnoreCase);

// Find --repo flags (can be repeated) and --since flag
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i].Equals("--repo", StringComparison.OrdinalIgnoreCase))
    {
        targetRepos.Add(args[i + 1]);
    }
    else if (args[i].Equals("--since", StringComparison.OrdinalIgnoreCase))
    {
        if (DateTimeOffset.TryParse(args[i + 1], out var parsedTimestamp))
        {
            sinceTimestamp = parsedTimestamp;
        }
        else
        {
            logger.LogError("Invalid timestamp format: {Timestamp}. Use ISO 8601 format (e.g., 2025-12-10T00:00:00Z)", args[i + 1]);
            return;
        }
    }
}

// Validate: --smart and --since are mutually exclusive
if (smartMode && sinceTimestamp.HasValue)
{
    logger.LogError("Cannot use --smart and --since together. Choose one.");
    return;
}

try
{
    if (smartMode)
    {
        logger.LogInformation("Smart sync mode - using stored last_synced_at timestamps");
    }
    else if (sinceTimestamp.HasValue)
    {
        logger.LogInformation("Incremental sync mode - only changes since {Timestamp:u}", sinceTimestamp.Value);
    }
    else
    {
        logger.LogInformation("Full sync mode - processing all issues");
    }

    if (targetRepos.Count > 0)
    {
        // Validate all repositories format first
        foreach (var repo in targetRepos)
        {
            var parts = repo.Split('/');
            if (parts.Length != 2)
            {
                logger.LogError("Invalid repository format: {Repo}. Expected 'Owner/Repo'", repo);
                return;
            }
        }
        // Sync specified repositories
        await syncService.SyncRepositoriesAsync(targetRepos, sinceTimestamp, smartMode);
    }
    else
    {
        // Sync all repositories (from config or dynamic discovery)
        await syncService.SyncAllRepositoriesAsync(sinceTimestamp, smartMode);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Sync failed");
    Environment.Exit(1);
}
