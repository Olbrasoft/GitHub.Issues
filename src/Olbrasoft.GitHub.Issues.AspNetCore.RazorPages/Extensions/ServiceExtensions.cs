namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// Coordinates registration across specialized extension classes following SRP.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Registers all GitHub Issues application services.
    /// Delegates to specialized extension classes for focused service registration.
    /// </summary>
    /// <param name="services">Service collection to register services with.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGitHubServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAiServices(configuration)           // Embeddings & Summarization (Cohere, OpenAI-compatible)
            .AddTranslationServices(configuration)  // Translation (Azure, DeepL, round-robin pool)
            .AddSearchServices(configuration)       // Search strategies & Issue search
            .AddSyncServices(configuration)         // GitHub sync services (repositories, issues, labels)
            .AddBusinessServices(configuration);    // Core business services (database, detail, cache, notifiers)
    }
}
