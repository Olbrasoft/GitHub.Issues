using Microsoft.Extensions.Logging;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Linux systemd-based service manager implementation.
/// Uses systemctl to manage user services.
///
/// For Windows support, implement a WindowsServiceManager that uses:
/// - sc.exe command for Windows services
/// - Or PowerShell commands (Start-Service, Get-Service)
/// </summary>
public class SystemdServiceManager : IServiceManager
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<SystemdServiceManager> _logger;

    public SystemdServiceManager(
        IProcessRunner processRunner,
        ILogger<SystemdServiceManager> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting service: {ServiceName}", serviceName);

        var result = await _processRunner.RunAsync(
            "systemctl",
            $"--user start {serviceName}",
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to start service {ServiceName}. Exit code: {ExitCode}. Error: {Error}",
                serviceName, result.ExitCode, result.StandardError);
        }
        else
        {
            _logger.LogInformation("Service {ServiceName} started", serviceName);
        }
    }

    public async Task<bool> IsServiceRunningAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            "systemctl",
            $"--user is-active {serviceName}",
            cancellationToken);

        var isActive = result.StandardOutput.Trim().Equals("active", StringComparison.OrdinalIgnoreCase);

        _logger.LogDebug("Service {ServiceName} is {Status}", serviceName, isActive ? "active" : "inactive");

        return isActive;
    }
}
