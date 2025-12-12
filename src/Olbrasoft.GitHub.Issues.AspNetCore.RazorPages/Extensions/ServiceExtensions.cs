using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions;
using Olbrasoft.GitHub.Issues.Text.Transformation.Cohere;
using Olbrasoft.GitHub.Issues.Text.Transformation.OpenAICompatible;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.DeepL;
using Olbrasoft.Mediation;
using EmbeddingSettings = Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.EmbeddingSettings;

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

        // Configure embedding provider settings from multiple possible locations
        ConfigureEmbeddingSettings(services, configuration);

        // Configure dedicated translation services
        services.Configure<AzureTranslatorSettings>(configuration.GetSection("AzureTranslator"));
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

        // Register embedding services with fallback
        AddEmbeddingServices(services, configuration);

        // Register core services
        services.AddHttpClient<GitHubGraphQLClient>();
        services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());

        // AI services - Text.Transformation
        services.AddHttpClient<ISummarizationService, OpenAICompatibleSummarizationService>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

        // Translation service (Azure Translator) for title and summary translations
        // Uses proper translation API, NOT LLM-based translation
        services.AddHttpClient<ITranslator, AzureTranslator>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        // Fallback translation service (DeepL) - used when Azure Translator fails
        // Note: Cohere was removed as fallback per Issue #209 - translations must use proper translators
        services.AddHttpClient<DeepLTranslator>()
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

        // Prompt loader for external markdown prompts
        services.AddSingleton<IPromptLoader, PromptLoader>();

        // Post-configure to load prompts from files (overrides appsettings if files exist)
        services.AddSingleton<IPostConfigureOptions<SummarizationSettings>, SummarizationPromptsPostConfigure>();

        return services;
    }

    /// <summary>
    /// Configures embedding provider settings from multiple possible config locations.
    /// </summary>
    private static void ConfigureEmbeddingSettings(IServiceCollection services, IConfiguration configuration)
    {
        // Configure Cohere settings - check multiple locations
        services.Configure<EmbeddingSettings>(options =>
        {
            // Try to get Cohere API keys from various locations
            var keys = new List<string>();

            // 1. TextTransformation:Embeddings:Cohere:ApiKeys
            var cohereSection = configuration.GetSection("TextTransformation:Embeddings:Cohere:ApiKeys");
            var arrayKeys = cohereSection.Get<string[]>() ?? [];
            keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));

            // 2. Embeddings:Cohere:ApiKeys (legacy nested)
            if (keys.Count == 0)
            {
                cohereSection = configuration.GetSection("Embeddings:Cohere:ApiKeys");
                arrayKeys = cohereSection.Get<string[]>() ?? [];
                keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 3. Embeddings:CohereApiKeys (legacy flat - Azure config style)
            if (keys.Count == 0)
            {
                cohereSection = configuration.GetSection("Embeddings:CohereApiKeys");
                arrayKeys = cohereSection.Get<string[]>() ?? [];
                keys.AddRange(arrayKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 4. AiProviders:Cohere:Keys
            if (keys.Count == 0)
            {
                var aiProviderKeys = configuration.GetSection("AiProviders:Cohere:Keys").Get<string[]>() ?? [];
                keys.AddRange(aiProviderKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 5. Single key fallbacks
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

        // Configure Voyage settings
        services.Configure<VoyageSettings>(options =>
        {
            options.ApiKey = configuration["Voyage:ApiKey"]
                          ?? configuration["VoyageApiKey"]
                          ?? configuration["Voyage__ApiKey"]
                          ?? "";
            options.Model = configuration["Voyage:Model"] ?? "voyage-multilingual-2";
        });

        // Configure Gemini settings
        services.Configure<GeminiSettings>(options =>
        {
            options.ApiKey = configuration["Gemini:ApiKey"]
                          ?? configuration["GeminiApiKey"]
                          ?? configuration["Gemini__ApiKey"]
                          ?? "";
            options.Model = configuration["Gemini:Model"] ?? "text-embedding-004";
        });
    }

    /// <summary>
    /// Registers embedding services with fallback: Cohere → Voyage → Gemini.
    /// </summary>
    private static void AddEmbeddingServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register individual embedding services
        services.AddHttpClient<CohereEmbeddingService>();
        services.AddHttpClient<VoyageEmbeddingService>();
        services.AddHttpClient<GeminiEmbeddingService>();

        // Register the fallback service as the main IEmbeddingService
        services.AddScoped<IEmbeddingService>(sp =>
        {
            var providers = new List<IEmbeddingService>();

            // Order: Cohere (1024d) → Voyage (1024d) → Gemini (768d)
            var cohereService = sp.GetRequiredService<CohereEmbeddingService>();
            var voyageService = sp.GetRequiredService<VoyageEmbeddingService>();
            var geminiService = sp.GetRequiredService<GeminiEmbeddingService>();

            providers.Add(cohereService);
            providers.Add(voyageService);
            providers.Add(geminiService);

            var logger = sp.GetRequiredService<ILogger<FallbackEmbeddingService>>();
            return new FallbackEmbeddingService(providers, logger);
        });
    }
}
