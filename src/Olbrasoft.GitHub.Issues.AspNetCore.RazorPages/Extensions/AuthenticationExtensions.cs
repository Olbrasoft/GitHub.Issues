using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for configuring GitHub OAuth authentication.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddGitHubAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var gitHubClientId = configuration["GitHub:ClientId"];
        var gitHubClientSecret = configuration["GitHub:ClientSecret"];
        var gitHubOwner = configuration["GitHub:Owner"] ?? "Olbrasoft";

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = "GitHub";
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
        });

        // GitHub OAuth authentication (optional - requires ClientSecret)
        if (!string.IsNullOrEmpty(gitHubClientSecret))
        {
            authBuilder.AddGitHub(options =>
            {
                options.ClientId = gitHubClientId ?? throw new InvalidOperationException("GitHub:ClientId not configured");
                options.ClientSecret = gitHubClientSecret;
                options.Scope.Add("read:user");
                options.CallbackPath = "/signin-github";
                options.SaveTokens = true;
            });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("OwnerOnly", policy =>
                policy.RequireAssertion(context =>
                {
                    var username = context.User.FindFirst(ClaimTypes.Name)?.Value
                                ?? context.User.FindFirst("urn:github:login")?.Value;
                    return string.Equals(username, gitHubOwner, StringComparison.OrdinalIgnoreCase);
                }));
        });

        return services;
    }
}
