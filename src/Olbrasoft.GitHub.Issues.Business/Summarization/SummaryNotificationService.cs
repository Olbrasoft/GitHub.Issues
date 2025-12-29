using Microsoft.Extensions.Logging;

namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Implementation of summary notification service.
/// Delegates to ISummaryNotifier for actual notification delivery.
/// </summary>
public class SummaryNotificationService : ISummaryNotificationService
{
    private readonly ISummaryNotifier _summaryNotifier;
    private readonly ILogger<SummaryNotificationService> _logger;

    public SummaryNotificationService(
        ISummaryNotifier summaryNotifier,
        ILogger<SummaryNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(summaryNotifier);
        ArgumentNullException.ThrowIfNull(logger);

        _summaryNotifier = summaryNotifier;
        _logger = logger;
    }

    public async Task NotifySummaryAsync(
        int issueId,
        string summary,
        string provider,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            _logger.LogWarning(
                "[SummaryNotificationService] Cannot notify - empty summary for issue {IssueId}",
                issueId);
            return;
        }

        _logger.LogDebug(
            "[SummaryNotificationService] Sending notification for issue {IssueId}, language={Language}, provider={Provider}",
            issueId, language, provider);

        var notification = new SummaryNotificationDto(
            issueId,
            summary,
            provider,
            language);

        await _summaryNotifier.NotifySummaryReadyAsync(notification, cancellationToken);

        _logger.LogInformation(
            "[SummaryNotificationService] Notification sent for issue {IssueId}",
            issueId);
    }
}
