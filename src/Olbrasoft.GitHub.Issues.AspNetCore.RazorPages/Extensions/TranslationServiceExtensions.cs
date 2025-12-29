using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Text.Translation;
using Olbrasoft.Text.Translation.Azure;
using Olbrasoft.Text.Translation.DeepL;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering translation services (Azure, DeepL, round-robin pool).
/// </summary>
public static class TranslationServiceExtensions
{
    public static IServiceCollection AddTranslationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure TextTransformation:Translation settings (new hierarchical structure)
        // Falls back to legacy flat structure if TextTransformation section doesn't exist
        var textTransformSection = configuration.GetSection("TextTransformation");
        if (textTransformSection.Exists())
        {
            services.Configure<TranslationSettings>(textTransformSection.GetSection("Translation"));
        }
        else
        {
            // Legacy flat configuration
            services.Configure<TranslationSettings>(configuration.GetSection("Translation"));
        }

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

        // Translation fallback service - now uses RoundRobinTranslator which handles fallback internally
        services.AddScoped<ITranslationFallbackService>(sp =>
        {
            var translator = sp.GetRequiredService<ITranslator>();
            var logger = sp.GetRequiredService<ILogger<TranslationFallbackService>>();
            // No separate fallback needed - RoundRobinTranslator handles rotation and fallback
            return new TranslationFallbackService(translator, logger, fallbackTranslator: null);
        });

        // Title translation service - uses RoundRobinTranslator which handles rotation and fallback
        // Updated to use ITranslationRepository for DIP compliance (issue #280)
        services.AddScoped<ITitleTranslationService, TitleTranslationService>();

        return services;
    }
}
