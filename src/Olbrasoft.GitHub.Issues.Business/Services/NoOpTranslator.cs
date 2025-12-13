using Olbrasoft.Text.Translation;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// No-operation translator that always returns failure.
/// Used when no translator API keys are configured.
/// </summary>
public class NoOpTranslator : ITranslator
{
    /// <inheritdoc />
    public Task<TranslatorResult> TranslateAsync(
        string text,
        string targetLanguage,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            TranslatorResult.Fail("No translator configured", "NoOp"));
    }
}
