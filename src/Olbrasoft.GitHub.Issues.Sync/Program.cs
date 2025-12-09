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
builder.Services.AddScoped<IGitHubSyncService, GitHubSyncService>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();
var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

// Parse command line arguments
if (args.Length == 0)
{
    logger.LogInformation("Usage:");
    logger.LogInformation("  sync              - Sync all configured repositories");
    logger.LogInformation("  sync owner/repo   - Sync specific repository");
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

            if (args.Length > 1)
            {
                var parts = args[1].Split('/');
                if (parts.Length != 2)
                {
                    logger.LogError("Invalid repository format. Expected 'owner/repo'");
                    return;
                }
                await syncService.SyncRepositoryAsync(parts[0], parts[1]);
            }
            else
            {
                await syncService.SyncAllRepositoriesAsync();
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
