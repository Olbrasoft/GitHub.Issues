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
    logger.LogInformation("  sync                                - Sync all repositories (incremental)");
    logger.LogInformation("  sync --full-refresh                 - Sync all repositories (full)");
    logger.LogInformation("  sync --repo Owner/Repo              - Sync single repository (incremental)");
    logger.LogInformation("  sync --repo Owner/Repo --full-refresh - Sync single repository (full)");
    logger.LogInformation("");
    logger.LogInformation("Options:");
    logger.LogInformation("  --repo Owner/Repo  Target a specific repository");
    logger.LogInformation("  --full-refresh     Ignore last sync timestamp, process all issues");
    return;
}

// Parse flags
var fullRefresh = args.Contains("--full-refresh", StringComparer.OrdinalIgnoreCase);
string? targetRepo = null;

// Find --repo flag and its value
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i].Equals("--repo", StringComparison.OrdinalIgnoreCase))
    {
        targetRepo = args[i + 1];
        break;
    }
}

try
{
    // Ensure Ollama is running before sync (auto-start if needed)
    await embeddingService.EnsureOllamaRunningAsync();

    if (fullRefresh)
    {
        logger.LogInformation("Full refresh mode enabled - ignoring last sync timestamps");
    }

    if (targetRepo != null)
    {
        // Sync single repository
        var parts = targetRepo.Split('/');
        if (parts.Length != 2)
        {
            logger.LogError("Invalid repository format: {Repo}. Expected 'Owner/Repo'", targetRepo);
            return;
        }
        await syncService.SyncRepositoryAsync(parts[0], parts[1], fullRefresh);
    }
    else
    {
        // Sync all repositories (from config or dynamic discovery)
        await syncService.SyncAllRepositoriesAsync(fullRefresh);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Sync failed");
    Environment.Exit(1);
}
