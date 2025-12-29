using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for registering database-related services.
/// </summary>
public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services)
    {
        // Database services (refactored for SRP - issue #316)
        services.AddScoped<IDatabaseHealthChecker, DatabaseHealthChecker>();
        services.AddScoped<IMigrationManager, MigrationManager>();
        services.AddScoped<IDatabaseStatusService, DatabaseStatusService>(); // Keep for backward compatibility

        return services;
    }
}
