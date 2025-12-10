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
if (args.Length == 0)
{
    logger.LogInformation("Usage:");
    logger.LogInformation("  sync [--full-refresh]                             - Sync all (incremental by default)");
    logger.LogInformation("  sync owner/repo [--full-refresh]                  - Sync single repository");
    logger.LogInformation("  sync owner/repo1 owner/repo2 ... [--full-refresh] - Sync list of repositories");
    logger.LogInformation("  reembed                                           - Re-embed all issues with title+body");
    logger.LogInformation("");
    logger.LogInformation("Options:");
    logger.LogInformation("  --full-refresh  Ignore last sync timestamp and re-sync everything");
    return;
}

var command = args[0].ToLowerInvariant();

// Check for --full-refresh flag
var fullRefresh = args.Contains("--full-refresh", StringComparer.OrdinalIgnoreCase);
var repoArgs = args.Where(a => !a.StartsWith("--") && a != command).ToList();

try
{
    switch (command)
    {
        case "sync":
            // Ensure Ollama is running before sync (auto-start if needed)
            await embeddingService.EnsureOllamaRunningAsync();

            if (fullRefresh)
            {
                logger.LogInformation("Full refresh mode enabled - ignoring last sync timestamps");
            }

            if (repoArgs.Count == 0)
            {
                // sync - use config list or dynamic discovery
                await syncService.SyncAllRepositoriesAsync(fullRefresh);
            }
            else if (repoArgs.Count == 1)
            {
                // sync owner/repo - single repository
                var parts = repoArgs[0].Split('/');
                if (parts.Length != 2)
                {
                    logger.LogError("Invalid repository format: {Repo}. Expected 'owner/repo'", repoArgs[0]);
                    return;
                }
                await syncService.SyncRepositoryAsync(parts[0], parts[1], fullRefresh);
            }
            else
            {
                // sync owner/repo1 owner/repo2 ... - multiple repositories
                var repositories = new List<string>();
                foreach (var repoArg in repoArgs)
                {
                    var parts = repoArg.Split('/');
                    if (parts.Length != 2)
                    {
                        logger.LogError("Invalid repository format: {Repo}. Expected 'owner/repo'", repoArg);
                        return;
                    }
                    repositories.Add(repoArg);
                }
                await syncService.SyncRepositoriesAsync(repositories, fullRefresh);
            }
            break;

        case "reembed":
            // Re-embed all issues with title+body (fetches body from GitHub API)
            await embeddingService.EnsureOllamaRunningAsync();
            await syncService.ReEmbedAllIssuesAsync();
            break;

        default:
            logger.LogError("Unknown command: {Command}", command);
            break;
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Sync failed");
    Environment.Exit(1);
}
