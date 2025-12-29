using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Business.GraphQL;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;
using Olbrasoft.Mediation;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering core business services (database, detail, summary, cache, notifiers).
/// </summary>
public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure business settings
        services.Configure<BodyPreviewSettings>(configuration.GetSection("BodyPreview"));
        services.Configure<AiProvidersSettings>(configuration.GetSection("AiProviders"));
        services.Configure<AiSummarySettings>(configuration.GetSection("AiSummary"));

        // Register Mediator and CQRS handlers
        services.AddMediation(typeof(Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries.IssueSearchQuery).Assembly)
            .UseRequestHandlerMediator();

        // TimeProvider for testable timestamps
        services.AddSingleton(TimeProvider.System);

        // Register GraphQL components (issue #279 - SRP refactoring)
        services.AddScoped<IGraphQLQueryBuilder, GraphQLQueryBuilder>();
        services.AddScoped<IGraphQLResponseParser, GraphQLResponseParser>();

        // Register core services
        services.AddHttpClient<GitHubGraphQLClient>();
        services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

        // Body preview generator (stateless)
        services.AddSingleton<IBodyPreviewGenerator, BodyPreviewGenerator>();

        // Summary cache service (refactored from IssueSummaryService for SRP - issue #310)
        services.AddScoped<ISummaryCacheService, SummaryCacheService>();

        // Issue summary service (summarization + translation + notification)
        services.AddScoped<IIssueSummaryService, IssueSummaryService>();

        // Issue detail services (refactored for SRP - issue #278)
        services.AddScoped<IIssueDetailQueryService, IssueDetailQueryService>();
        services.AddScoped<IIssueBodyFetchService, IssueBodyFetchService>();
        services.AddScoped<IIssueSummaryOrchestrator, IssueSummaryOrchestrator>();
        services.AddScoped<IIssueDetailService, IssueDetailService>(); // Keep for backward compatibility

        // Database services (refactored for SRP - issue #316)
        services.AddScoped<IDatabaseHealthChecker, DatabaseHealthChecker>();
        services.AddScoped<IMigrationManager, MigrationManager>();
        services.AddScoped<IDatabaseStatusService, DatabaseStatusService>(); // Keep for backward compatibility

        // Translation cache services (#262)
        services.AddScoped<ITranslationCacheService, TranslationCacheService>();
        services.AddScoped<ITranslatedTextService, TranslatedTextService>();

        // SignalR notifiers
        services.AddScoped<ISummaryNotifier, SignalRSummaryNotifier>();
        services.AddScoped<IBodyNotifier, SignalRBodyNotifier>();
        services.AddScoped<ITitleTranslationNotifier, SignalRTitleTranslationNotifier>();

        return services;
    }
}
