using Microsoft.Extensions.Options;
using Olbrasoft.Text.Transformation.Abstractions;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

/// <summary>
/// Post-configures SummarizationSettings by loading prompts from external markdown files.
/// Prompts from files override the values from appsettings.json.
/// </summary>
public class SummarizationPromptsPostConfigure : IPostConfigureOptions<SummarizationSettings>
{
    private readonly IPromptLoader _promptLoader;
    private readonly ILogger<SummarizationPromptsPostConfigure> _logger;

    public SummarizationPromptsPostConfigure(
        IPromptLoader promptLoader,
        ILogger<SummarizationPromptsPostConfigure> logger)
    {
        _promptLoader = promptLoader;
        _logger = logger;
    }

    public void PostConfigure(string? name, SummarizationSettings options)
    {
        var systemPrompt = _promptLoader.GetPrompt("summarization-system");
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            options.SystemPrompt = systemPrompt;
            _logger.LogInformation("Loaded summarization system prompt from file");
        }
        else
        {
            _logger.LogDebug("Using default summarization system prompt from appsettings");
        }
    }
}
