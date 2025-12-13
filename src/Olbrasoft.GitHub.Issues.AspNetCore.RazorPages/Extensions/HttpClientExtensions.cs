using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;
using Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for configuring HttpClient services for GitHub API.
/// </summary>
public static class HttpClientExtensions
{
    public static IServiceCollection AddGitHubHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repository API client
        services.AddHttpClient<IGitHubRepositoryApiClient, GitHubRepositoryApiClient>(ConfigureGitHubClient)
            .ConfigureHttpClient((sp, client) => AddGitHubAuthorization(sp, client));

        services.AddScoped<IRepositorySyncService, RepositorySyncService>();

        // Issue API client
        services.AddHttpClient<IGitHubIssueApiClient, GitHubIssueApiClient>(ConfigureGitHubClient)
            .ConfigureHttpClient((sp, client) => AddGitHubAuthorization(sp, client));

        // Issue embedding generator (shared between sync and webhook services)
        services.AddScoped<IIssueEmbeddingGenerator, IssueEmbeddingGenerator>();
        services.AddScoped<IIssueSyncService, IssueSyncService>();

        // Event API client
        services.AddHttpClient<IGitHubEventApiClient, GitHubEventApiClient>(ConfigureGitHubClient)
            .ConfigureHttpClient((sp, client) => AddGitHubAuthorization(sp, client));

        services.AddScoped<IEventSyncService, EventSyncService>();

        // Webhook services
        services.Configure<WebhookSettings>(configuration.GetSection("GitHubApp"));
        services.AddSingleton<IWebhookSignatureValidator, WebhookSignatureValidator>();
        services.AddScoped<IIssueUpdateNotifier, Hubs.SignalRIssueUpdateNotifier>();
        services.AddScoped<ISearchResultNotifier, Hubs.SignalRSearchResultNotifier>();

        // Webhook event handlers (Strategy Pattern)
        services.AddScoped<IWebhookEventHandler<GitHubIssueWebhookPayload>, IssueEventHandler>();
        services.AddScoped<IWebhookEventHandler<GitHubIssueCommentWebhookPayload>, IssueCommentEventHandler>();
        services.AddScoped<IWebhookEventHandler<GitHubRepositoryWebhookPayload>, RepositoryEventHandler>();
        services.AddScoped<IWebhookEventHandler<GitHubLabelWebhookPayload>, LabelEventHandler>();

        // Webhook orchestrator
        services.AddScoped<IGitHubWebhookService, GitHubWebhookService>();

        return services;
    }

    private static void ConfigureGitHubClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Olbrasoft-GitHub-Issues-Sync", "1.0"));
    }

    private static void AddGitHubAuthorization(IServiceProvider sp, HttpClient client)
    {
        var settings = sp.GetRequiredService<IOptions<GitHubSettings>>();
        var logger = sp.GetService<ILogger<GitHubSettings>>();

        if (!string.IsNullOrEmpty(settings.Value.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.Token);
            logger?.LogDebug("GitHub API client configured with Bearer token (length: {Length})", settings.Value.Token.Length);
        }
        else
        {
            logger?.LogWarning("GitHub API client configured WITHOUT authentication token. " +
                "Set 'GitHub__Token' environment variable or 'GitHub:Token' in appsettings.json. " +
                "Without token, API rate limit is 60 requests/hour.");
        }
    }
}
