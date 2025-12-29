using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering GitHub sync services (repositories, issues, labels, events).
/// </summary>
public static class SyncServiceExtensions
{
    public static IServiceCollection AddSyncServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure GitHub and sync settings
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));

        // GitHub API client
        services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();

        // Sync business services
        services.AddScoped<IIssueSyncBusinessService, IssueSyncBusinessService>();
        services.AddScoped<ILabelSyncBusinessService, LabelSyncBusinessService>();
        services.AddScoped<IRepositorySyncBusinessService, RepositorySyncBusinessService>();
        services.AddScoped<IEventSyncBusinessService, EventSyncBusinessService>();
        services.AddScoped<ILabelSyncService, LabelSyncService>();
        services.AddScoped<IGitHubSyncService, GitHubSyncService>();

        return services;
    }
}
