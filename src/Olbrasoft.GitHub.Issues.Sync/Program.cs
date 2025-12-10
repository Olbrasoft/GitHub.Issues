using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Sync.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

// Configure DbContext
builder.Services.AddDbContext<GitHubDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));

// Configure embedding service
builder.Services.Configure<EmbeddingSettings>(builder.Configuration.GetSection("Embeddings"));
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();

// Configure GitHub sync service
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection("GitHub"));
builder.Services.AddHttpClient<IGitHubSyncService, GitHubSyncService>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();
var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

// Parse command line arguments
if (args.Length == 0 || args[0].ToLowerInvariant() != "sync")
{
    logger.LogInformation("Usage:");
    logger.LogInformation("  sync                                        - Full sync of all repositories");
    logger.LogInformation("  sync --repo Owner/Repo                      - Full sync of specific repository");
    logger.LogInformation("  sync --repo X --repo Y                      - Full sync of multiple repositories");
    logger.LogInformation("  sync --since 2025-12-10T00:00:00Z           - Incremental sync (changes since timestamp)");
    logger.LogInformation("  sync --since 2025-12-10T00:00:00Z --repo X  - Incremental sync of specific repo");
    logger.LogInformation("");
    logger.LogInformation("Options:");
    logger.LogInformation("  --repo Owner/Repo     Target specific repository (can be repeated)");
    logger.LogInformation("  --since TIMESTAMP     Incremental sync: only issues changed since TIMESTAMP (ISO 8601)");
    return;
}

// Parse flags
var targetRepos = new List<string>();
DateTimeOffset? sinceTimestamp = null;

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

try
{
    // Ensure Ollama is running before sync (auto-start if needed)
    await embeddingService.EnsureOllamaRunningAsync();

    if (sinceTimestamp.HasValue)
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
        await syncService.SyncRepositoriesAsync(targetRepos, sinceTimestamp);
    }
    else
    {
        // Sync all repositories (from config or dynamic discovery)
        await syncService.SyncAllRepositoriesAsync(sinceTimestamp);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Sync failed");
    Environment.Exit(1);
}
