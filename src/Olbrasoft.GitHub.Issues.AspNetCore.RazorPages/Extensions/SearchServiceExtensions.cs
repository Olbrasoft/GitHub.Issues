using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Business.Strategies;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering search-related services (strategies, search service).
/// </summary>
public static class SearchServiceExtensions
{
    public static IServiceCollection AddSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure search settings
        services.Configure<SearchSettings>(configuration.GetSection("Search"));
        services.Configure<SyncSettings>(configuration.GetSection("Sync"));

        // Search strategies (Strategy Pattern)
        services.AddScoped<ISearchStrategy, ExactMatchSearchStrategy>();
        services.AddScoped<ISearchStrategy, SemanticSearchStrategy>();
        services.AddScoped<ISearchStrategy, RepositoryBrowseStrategy>();

        // Issue search service
        services.AddScoped<IIssueSearchService, IssueSearchService>();

        // Embedding text builder
        services.AddSingleton<IEmbeddingTextBuilder>(sp =>
        {
            var syncSettings = sp.GetRequiredService<IOptions<SyncSettings>>();
            return new EmbeddingTextBuilder(syncSettings.Value.MaxEmbeddingTextLength);
        });

        return services;
    }
}
