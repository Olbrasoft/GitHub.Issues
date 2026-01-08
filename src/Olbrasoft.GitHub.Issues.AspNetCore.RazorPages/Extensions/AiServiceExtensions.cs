using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Configuration;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Transformation.Cohere;
using Olbrasoft.Text.Transformation.OpenAICompatible;
using EmbeddingSettings = Olbrasoft.Text.Transformation.Abstractions.EmbeddingSettings;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering AI services (embeddings, summarization).
/// </summary>
public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure embedding settings
        var textTransformSection = configuration.GetSection("TextTransformation");
        if (textTransformSection.Exists())
        {
            services.Configure<EmbeddingSettings>(textTransformSection.GetSection("Embeddings"));
            services.Configure<SummarizationSettings>(textTransformSection.GetSection("Summarization"));
        }
        else
        {
            // Legacy flat configuration
            services.Configure<EmbeddingSettings>(configuration.GetSection("Embeddings"));
            services.Configure<SummarizationSettings>(configuration.GetSection("Summarization"));
        }

        // Configure Cohere API keys from multiple possible locations
        services.PostConfigure<EmbeddingSettings>(options =>
        {
            // Try to get Cohere API keys from various locations
            var keys = new List<string>();

            // 1. TextTransformation:Embeddings:Cohere:ApiKeys (array from appsettings.json)
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

            // 3. AiProviders:Cohere:Keys (array from appsettings.json)
            if (keys.Count == 0)
            {
                var aiProviderKeys = configuration.GetSection("AiProviders:Cohere:Keys").Get<string[]>() ?? [];
                keys.AddRange(aiProviderKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // 4. Individual keys from SecureStore (AiProviders:Cohere:Key1, Key2, etc.)
            if (keys.Count == 0)
            {
                keys.AddRange(ConfigurationKeyLoader.LoadNumberedKeys(configuration, "AiProviders:Cohere:Key"));
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

        // Register Cohere embedding service (primary)
        services.AddHttpClient<CohereEmbeddingService>();
        services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<CohereEmbeddingService>());

        // AI services - Text.Transformation
        services.AddHttpClient<ISummarizationService, OpenAICompatibleSummarizationService>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

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
