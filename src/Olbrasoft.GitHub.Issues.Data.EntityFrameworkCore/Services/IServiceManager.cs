namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Abstraction for managing system services.
/// Different implementations for different platforms (systemd, Windows Services, etc.).
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Starts a system service.
    /// </summary>
    /// <param name="serviceName">Name of the service to start.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a system service is running.
    /// </summary>
    /// <param name="serviceName">Name of the service to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is running.</returns>
    Task<bool> IsServiceRunningAsync(string serviceName, CancellationToken cancellationToken = default);
}
