using Microsoft.Extensions.FileProviders;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

/// <summary>
/// Loads LLM prompts from markdown files in the Prompts directory.
/// Caches prompts and watches for file changes in development.
/// </summary>
public interface IPromptLoader
{
    /// <summary>
    /// Gets a prompt by name (without extension).
    /// </summary>
    /// <param name="promptName">Name of the prompt file (e.g., "summarization-system")</param>
    /// <returns>The prompt content, or null if not found</returns>
    string? GetPrompt(string promptName);

    /// <summary>
    /// Gets a prompt and replaces placeholders with values.
    /// </summary>
    /// <param name="promptName">Name of the prompt file</param>
    /// <param name="replacements">Dictionary of placeholder -> value replacements</param>
    /// <returns>The prompt content with replacements applied</returns>
    string? GetPrompt(string promptName, IDictionary<string, string> replacements);
}

public class PromptLoader : IPromptLoader
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PromptLoader> _logger;
    private readonly Dictionary<string, string> _cache = new();
    private readonly object _cacheLock = new();
    private readonly string _promptsPath;

    public PromptLoader(IWebHostEnvironment environment, ILogger<PromptLoader> logger)
    {
        _environment = environment;
        _logger = logger;

        // Try multiple paths for Azure compatibility
        var possiblePaths = new[]
        {
            Path.Combine(_environment.ContentRootPath, "Prompts"),
            Path.Combine(AppContext.BaseDirectory, "Prompts"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts")
        };

        _promptsPath = possiblePaths.FirstOrDefault(Directory.Exists)
            ?? possiblePaths[0]; // Fallback to first option

        _logger.LogInformation("PromptLoader using path: {Path} (exists: {Exists})",
            _promptsPath, Directory.Exists(_promptsPath));

        // In development, set up file watcher for hot reload
        if (_environment.IsDevelopment())
        {
            SetupFileWatcher();
        }
    }

    public string? GetPrompt(string promptName)
    {
        lock (_cacheLock)
        {
            // Check cache first (in production, always use cache)
            if (_cache.TryGetValue(promptName, out var cached))
            {
                return cached;
            }

            // Load from file
            var filePath = Path.Combine(_promptsPath, $"{promptName}.md");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Prompt file not found: {Path}", filePath);
                return null;
            }

            try
            {
                var content = File.ReadAllText(filePath).Trim();
                _cache[promptName] = content;
                _logger.LogInformation("Loaded prompt from file: {PromptName}", promptName);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load prompt: {PromptName}", promptName);
                return null;
            }
        }
    }

    public string? GetPrompt(string promptName, IDictionary<string, string> replacements)
    {
        var prompt = GetPrompt(promptName);
        if (prompt == null)
        {
            return null;
        }

        foreach (var (key, value) in replacements)
        {
            prompt = prompt.Replace($"{{{{{key}}}}}", value);
        }

        return prompt;
    }

    private void SetupFileWatcher()
    {
        try
        {
            if (!Directory.Exists(_promptsPath))
            {
                return;
            }

            var watcher = new FileSystemWatcher(_promptsPath, "*.md")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnPromptFileChanged;
            watcher.Created += OnPromptFileChanged;

            _logger.LogInformation("Prompt file watcher enabled for development");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up prompt file watcher");
        }
    }

    private void OnPromptFileChanged(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name))
        {
            return;
        }

        var promptName = Path.GetFileNameWithoutExtension(e.Name);

        lock (_cacheLock)
        {
            _cache.Remove(promptName);
        }

        _logger.LogInformation("Prompt cache cleared for: {PromptName}", promptName);
    }
}
