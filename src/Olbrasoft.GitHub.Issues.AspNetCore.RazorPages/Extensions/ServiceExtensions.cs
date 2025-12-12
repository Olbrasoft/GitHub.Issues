using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;
using Olbrasoft.GitHub.Issues.Text.Transformation.Ollama;
using Olbrasoft.GitHub.Issues.Text.Transformation.Cohere;
using Olbrasoft.GitHub.Issues.Text.Transformation.OpenAICompatible;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.DeepL;
using Olbrasoft.Mediation;
using EmbeddingSettings = Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.EmbeddingSettings;
using IServiceManager = Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.IServiceManager;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure TextTransformation settings (new hierarchical structure)
        // Falls back to legacy flat structure if TextTransformation section doesn't exist
        var textTransformSection = configuration.GetSection("TextTransformation");
        if (textTransformSection.Exists())
        {
            services.Configure<EmbeddingSettings>(textTransformSection.GetSection("Embeddings"));
            services.Configure<SummarizationSettings>(textTransformSection.GetSection("Summarization"));
            services.Configure<TranslationSettings>(textTransformSection.GetSection("Translation"));
        }
        else
        {
            // Legacy flat configuration
            services.Configure<EmbeddingSettings>(configuration.GetSection("Embeddings"));
            services.Configure<SummarizationSettings>(configuration.GetSection("Summarization"));
            services.Configure<TranslationSettings>(configuration.GetSection("Translation"));
        }

        // Configure dedicated translation service (DeepL)
        services.Configure<DeepLSettings>(configuration.GetSection("DeepL"));

        // Other settings (unchanged)
        services.Configure<SearchSettings>(configuration.GetSection("Search"));
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));
        services.Configure<BodyPreviewSettings>(configuration.GetSection("BodyPreview"));
        services.Configure<AiProvidersSettings>(configuration.GetSection("AiProviders"));
        services.Configure<AiSummarySettings>(configuration.GetSection("AiSummary"));
        services.Configure<SyncSettings>(configuration.GetSection("Sync"));

        // Register Mediator and CQRS handlers
        services.AddMediation(typeof(Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries.IssueSearchQuery).Assembly)
            .UseRequestHandlerMediator();

        // Register embedding service based on provider
        var embeddingSection = textTransformSection.Exists()
            ? textTransformSection.GetSection("Embeddings")
            : configuration.GetSection("Embeddings");
        var embeddingSettings = embeddingSection.Get<EmbeddingSettings>() ?? new EmbeddingSettings();
        services.AddEmbeddingServices(embeddingSettings);

        // Register core services
        services.AddHttpClient<GitHubGraphQLClient>();
        services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

        // AI services - Text.Transformation
        services.AddHttpClient<ISummarizationService, OpenAICompatibleSummarizationService>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

        // Translation service (DeepL) for title and summary translations
        services.AddHttpClient<ITranslator, DeepLTranslator>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        // Business services
        services.AddScoped<IIssueSearchService, IssueSearchService>();
        services.AddScoped<IIssueDetailService, IssueDetailService>();
        services.AddScoped<IDatabaseStatusService, DatabaseStatusService>();

        // SignalR notifiers
        services.AddScoped<ISummaryNotifier, SignalRSummaryNotifier>();
        services.AddScoped<IBodyNotifier, SignalRBodyNotifier>();
        services.AddScoped<ITitleTranslationNotifier, SignalRTitleTranslationNotifier>();
        services.AddScoped<ITitleTranslationService, TitleTranslationService>();

        // Sync services
        services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();
        services.AddScoped<IIssueSyncBusinessService, IssueSyncBusinessService>();
        services.AddScoped<ILabelSyncBusinessService, LabelSyncBusinessService>();
        services.AddScoped<IRepositorySyncBusinessService, RepositorySyncBusinessService>();
        services.AddScoped<IEventSyncBusinessService, EventSyncBusinessService>();
        services.AddScoped<ILabelSyncService, LabelSyncService>();
        services.AddScoped<IGitHubSyncService, GitHubSyncService>();

        // Embedding text builder
        services.AddSingleton<IEmbeddingTextBuilder>(sp =>
        {
            var syncSettings = sp.GetRequiredService<IOptions<SyncSettings>>();
            return new EmbeddingTextBuilder(syncSettings.Value.MaxEmbeddingTextLength);
        });

        return services;
    }

    private static IServiceCollection AddEmbeddingServices(
        this IServiceCollection services,
        EmbeddingSettings embeddingSettings)
    {
        if (embeddingSettings.Provider == EmbeddingProvider.Cohere)
        {
            services.AddHttpClient<CohereEmbeddingService>();
            services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<CohereEmbeddingService>());
        }
        else
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IServiceManager, SystemdServiceManager>();
            services.AddHttpClient<OllamaEmbeddingService>();
            services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
            services.AddScoped<IServiceLifecycleManager>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
        }

        return services;
    }
}
