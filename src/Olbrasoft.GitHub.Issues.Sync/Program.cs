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
    logger.LogInformation("  sync                             - Sync all (config list OR dynamic discovery)");
    logger.LogInformation("  sync owner/repo                  - Sync single repository");
    logger.LogInformation("  sync owner/repo1 owner/repo2 ... - Sync list of repositories");
    return;
}

var command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "sync":
            // Ensure Ollama is running before sync (auto-start if needed)
            await embeddingService.EnsureOllamaRunningAsync();

            if (args.Length == 1)
            {
                // sync - use config list or dynamic discovery
                await syncService.SyncAllRepositoriesAsync();
            }
            else if (args.Length == 2)
            {
                // sync owner/repo - single repository
                var parts = args[1].Split('/');
                if (parts.Length != 2)
                {
                    logger.LogError("Invalid repository format: {Repo}. Expected 'owner/repo'", args[1]);
                    return;
                }
                await syncService.SyncRepositoryAsync(parts[0], parts[1]);
            }
            else
            {
                // sync owner/repo1 owner/repo2 ... - multiple repositories
                var repositories = new List<string>();
                for (var i = 1; i < args.Length; i++)
                {
                    var parts = args[i].Split('/');
                    if (parts.Length != 2)
                    {
                        logger.LogError("Invalid repository format: {Repo}. Expected 'owner/repo'", args[i]);
                        return;
                    }
                    repositories.Add(args[i]);
                }
                await syncService.SyncRepositoriesAsync(repositories);
            }
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
