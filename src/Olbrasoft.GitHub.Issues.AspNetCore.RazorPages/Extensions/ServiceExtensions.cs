using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Business.Strategies;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;
using Olbrasoft.Text.Transformation.OpenAICompatible;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.DeepL;
using Olbrasoft.Mediation;
using EmbeddingSettings = Olbrasoft.Text.Transformation.Abstractions.EmbeddingSettings;

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

        // Configure Cohere API keys from multiple possible locations
        services.PostConfigure<EmbeddingSettings>(options =>
        {
            // Try to get Cohere API keys from various locations
            var keys = new List<string>();

            // 1. TextTransformation:Embeddings:Cohere:ApiKeys
            var cohereSection = configuration.GetSection("TextTransformation:Embeddings:Cohere:ApiKeys");
            var arrayKeys = cohereSection.Get<string[]>() ?? [];
            keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));

            // 2. Embeddings:CohereApiKeys (Azure config style)
            if (keys.Count == 0)
            {
                cohereSection = configuration.GetSection("Embeddings:CohereApiKeys");
                arrayKeys = cohereSection.Get<string[]>() ?? [];
                keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 3. AiProviders:Cohere:Keys
            if (keys.Count == 0)
            {
                var aiProviderKeys = configuration.GetSection("AiProviders:Cohere:Keys").Get<string[]>() ?? [];
                keys.AddRange(aiProviderKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 4. Single key fallbacks
            if (keys.Count == 0)
            {
                var singleKey = configuration["Embeddings:CohereApiKey"]
                             ?? configuration["CohereApiKey"]
                             ?? configuration["Cohere__ApiKey"];
                if (!string.IsNullOrWhiteSpace(singleKey))
                {
                    keys.Add(singleKey);
                }
            }

            if (keys.Count > 0)
            {
                options.Cohere.ApiKeys = keys.ToArray();
            }
        });

        // Configure dedicated translation services (legacy single-key config)
        services.Configure<AzureTranslatorSettings>(configuration.GetSection("AzureTranslator"));
        services.Configure<DeepLSettings>(configuration.GetSection("DeepL"));

        // Configure translator pool for round-robin multi-key support
        services.Configure<TranslatorPoolSettings>(configuration.GetSection("TranslatorPool"));

        // Post-configure to collect API keys from multiple configuration sources
        services.PostConfigure<TranslatorPoolSettings>(options =>
        {
            // Collect Azure API keys from various sources
            var azureKeys = new List<string>();

            // 1. TranslatorPool:AzureApiKeys (array)
            var poolAzureKeys = configuration.GetSection("TranslatorPool:AzureApiKeys").Get<string[]>() ?? [];
            azureKeys.AddRange(poolAzureKeys.Where(k => !string.IsNullOrWhiteSpace(k)));

            // 2. Fallback to single AzureTranslator:ApiKey
            if (azureKeys.Count == 0)
            {
                var singleKey = configuration["AzureTranslator:ApiKey"]
                             ?? configuration["AzureTranslator__ApiKey"];
                if (!string.IsNullOrWhiteSpace(singleKey))
                {
                    azureKeys.Add(singleKey);
                }
            }

            options.AzureApiKeys = azureKeys.ToArray();

            // Get Azure region/endpoint from config or keep defaults
            var azureRegion = configuration["AzureTranslator:Region"]
                           ?? configuration["TranslatorPool:AzureRegion"];
            if (!string.IsNullOrWhiteSpace(azureRegion))
            {
                options.AzureRegion = azureRegion;
            }

            // Collect DeepL API keys from various sources
            var deepLKeys = new List<string>();

            // 1. TranslatorPool:DeepLApiKeys (array)
            var poolDeepLKeys = configuration.GetSection("TranslatorPool:DeepLApiKeys").Get<string[]>() ?? [];
            deepLKeys.AddRange(poolDeepLKeys.Where(k => !string.IsNullOrWhiteSpace(k)));

            // 2. Fallback to single DeepL:ApiKey
            if (deepLKeys.Count == 0)
            {
                var singleKey = configuration["DeepL:ApiKey"]
                             ?? configuration["DeepL__ApiKey"];
                if (!string.IsNullOrWhiteSpace(singleKey))
                {
                    deepLKeys.Add(singleKey);
                }
            }

            options.DeepLApiKeys = deepLKeys.ToArray();
        });

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

        // Register Cohere embedding service (primary)
        services.AddHttpClient<CohereEmbeddingService>();
        services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<CohereEmbeddingService>());

        // Register core services
        services.AddHttpClient<GitHubGraphQLClient>();
        services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

        // AI services - Text.Transformation
        services.AddHttpClient<ISummarizationService, OpenAICompatibleSummarizationService>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

        // Translation service - Round-robin pool with multiple providers and keys
        // Distributes load across Azure and DeepL with automatic fallback
        services.AddSingleton<TranslatorPoolBuilder>();

        services.AddSingleton<ITranslator>(sp =>
        {
            var poolSettings = sp.GetRequiredService<IOptions<TranslatorPoolSettings>>().Value;

            // Check if pool has any translators configured
            if (!poolSettings.HasAnyTranslators)
            {
                var logger = sp.GetRequiredService<ILogger<RoundRobinTranslator>>();
                logger.LogWarning("[Translation] No translator API keys configured. Translation will fail.");

                // Return a no-op translator that always fails
                return new NoOpTranslator();
            }

            var builder = sp.GetRequiredService<TranslatorPoolBuilder>();
            var providerGroups = builder.BuildProviderGroups();
            var poolLogger = sp.GetRequiredService<ILogger<RoundRobinTranslator>>();

            return new RoundRobinTranslator(providerGroups, poolLogger);
        });

        // Search strategies (Strategy Pattern)
        services.AddScoped<ISearchStrategy, ExactMatchSearchStrategy>();
        services.AddScoped<ISearchStrategy, SemanticSearchStrategy>();
        services.AddScoped<ISearchStrategy, RepositoryBrowseStrategy>();

        // Body preview generator (stateless)
        services.AddSingleton<IBodyPreviewGenerator, BodyPreviewGenerator>();

        // Translation fallback service - now uses RoundRobinTranslator which handles fallback internally
        services.AddScoped<ITranslationFallbackService>(sp =>
        {
            var translator = sp.GetRequiredService<ITranslator>();
            var logger = sp.GetRequiredService<ILogger<TranslationFallbackService>>();
            // No separate fallback needed - RoundRobinTranslator handles rotation and fallback
            return new TranslationFallbackService(translator, logger, fallbackTranslator: null);
        });

        // Issue summary service (summarization + translation + notification)
        services.AddScoped<IIssueSummaryService, IssueSummaryService>();

        // Business services
        services.AddScoped<IIssueSearchService, IssueSearchService>();
        services.AddScoped<IIssueDetailService, IssueDetailService>();
        services.AddScoped<IDatabaseStatusService, DatabaseStatusService>();

        // SignalR notifiers
        services.AddScoped<ISummaryNotifier, SignalRSummaryNotifier>();
        services.AddScoped<IBodyNotifier, SignalRBodyNotifier>();
        services.AddScoped<ITitleTranslationNotifier, SignalRTitleTranslationNotifier>();

        // Title translation service - uses RoundRobinTranslator which handles rotation and fallback
        services.AddScoped<ITitleTranslationService>(sp =>
        {
            var dbContext = sp.GetRequiredService<GitHubDbContext>();
            var translator = sp.GetRequiredService<ITranslator>();
            var notifier = sp.GetRequiredService<ITitleTranslationNotifier>();
            var logger = sp.GetRequiredService<ILogger<TitleTranslationService>>();
            // No separate fallback - RoundRobinTranslator handles it
            return new TitleTranslationService(dbContext, translator, notifier, logger, fallbackTranslator: null);
        });

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

        // Prompt loader for external markdown prompts
        services.AddSingleton<IPromptLoader, PromptLoader>();

        // Post-configure to load prompts from files (overrides appsettings if files exist)
        services.AddSingleton<IPostConfigureOptions<SummarizationSettings>, SummarizationPromptsPostConfigure>();

        return services;
    }
}
