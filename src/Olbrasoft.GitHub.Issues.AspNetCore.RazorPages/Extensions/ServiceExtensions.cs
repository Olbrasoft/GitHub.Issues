using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.GraphQL;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Business.Detail;
using Olbrasoft.GitHub.Issues.Business.Summarization;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// Delegates to focused extension classes for different service areas.
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register services by functional area (issue #313 - SRP refactoring)
        // NOTE: Order matters! AddGitHubSyncServices must be called first because
        // AddAiServices depends on SyncSettings configured in sync services.
        services.AddGitHubSyncServices(configuration);
        services.AddAiServices(configuration);
        services.AddTranslationServices(configuration);
        services.AddSearchServices(configuration);
        services.AddDatabaseServices();

        // Configure remaining settings
        services.Configure<BodyPreviewSettings>(configuration.GetSection("BodyPreview"));
        services.Configure<AiSummarySettings>(configuration.GetSection("AiSummary"));

        // Register Mediator and CQRS handlers
        services.AddMediation(typeof(Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries.IssueSearchQuery).Assembly)
            .UseRequestHandlerMediator();

        // Register GraphQL components (issue #279 - SRP refactoring)
        services.AddScoped<IGraphQLQueryBuilder, GraphQLQueryBuilder>();
        services.AddScoped<IGraphQLResponseParser, GraphQLResponseParser>();

        // Register core services
        services.AddHttpClient<GitHubGraphQLClient>();
        services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

        // TimeProvider for testable timestamps
        services.AddSingleton(TimeProvider.System);

        // Body preview generator (stateless)
        services.AddSingleton<IBodyPreviewGenerator, BodyPreviewGenerator>();

        // Issue detail services (refactored for SRP - issue #278)
        services.AddScoped<IIssueDetailQueryService, IssueDetailQueryService>();
        services.AddScoped<IIssueBodyFetchService, IssueBodyFetchService>();
        services.AddScoped<IIssueSummaryOrchestrator, IssueSummaryOrchestrator>();
        services.AddScoped<IIssueDetailService, IssueDetailService>(); // Keep for backward compatibility

        // Issue summary service (summarization + translation + notification)
        services.AddScoped<IIssueSummaryService, IssueSummaryService>();

        // SignalR notifiers
        services.AddScoped<ISummaryNotifier, SignalRSummaryNotifier>();
        services.AddScoped<IBodyNotifier, SignalRBodyNotifier>();

        return services;
    }
}
