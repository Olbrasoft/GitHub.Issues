using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Search;
using Olbrasoft.GitHub.Issues.Business.Search.Strategies;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering search-related services.
/// </summary>
public static class SearchServiceExtensions
{
    public static IServiceCollection AddSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure search settings
        services.Configure<SearchSettings>(configuration.GetSection("Search"));

        // Search strategies (Strategy Pattern)
        services.AddScoped<ISearchStrategy, ExactMatchSearchStrategy>();
        services.AddScoped<ISearchStrategy, SemanticSearchStrategy>();
        services.AddScoped<ISearchStrategy, RepositoryBrowseStrategy>();

        // Business service for issue search
        services.AddScoped<IIssueSearchService, IssueSearchService>();

        return services;
    }
}
