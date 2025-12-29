using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering GitHub synchronization services.
/// </summary>
public static class SyncServiceExtensions
{
    public static IServiceCollection AddGitHubSyncServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure GitHub and sync settings
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));
        services.Configure<SyncSettings>(configuration.GetSection("Sync"));

        // GitHub API client (Octokit wrapper)
        services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();

        // Sync business services
        services.AddScoped<IIssueSyncBusinessService, IssueSyncBusinessService>();
        services.AddScoped<ILabelSyncBusinessService, LabelSyncBusinessService>();
        services.AddScoped<IRepositorySyncBusinessService, RepositorySyncBusinessService>();
        services.AddScoped<IEventSyncBusinessService, EventSyncBusinessService>();

        // Sync orchestration services
        services.AddScoped<ILabelSyncService, LabelSyncService>();
        services.AddScoped<IGitHubSyncService, GitHubSyncService>();

        return services;
    }
}
