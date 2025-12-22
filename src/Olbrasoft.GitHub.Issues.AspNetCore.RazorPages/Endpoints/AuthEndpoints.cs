using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// Authentication-related endpoints.
/// </summary>
public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/login", async (HttpContext context, IConfiguration config) =>
        {
            var gitHubClientSecret = config["GitHub:ClientSecret"];

            // GitHub OAuth není nakonfigurovaný - nelze se přihlásit
            if (string.IsNullOrEmpty(gitHubClientSecret))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "GitHub OAuth není nakonfigurovaný",
                    message = "Pro přihlášení je potřeba nastavit GitHub:ClientSecret"
                });
                return;
            }

            var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            await context.ChallengeAsync("GitHub", new AuthenticationProperties
            {
                RedirectUri = returnUrl
            });
        });

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/");
        });

        app.MapGet("/api/auth/status", (HttpContext context, IConfiguration config) =>
        {
            var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value
                        ?? context.User.FindFirst("urn:github:login")?.Value;
            var owner = config["GitHub:Owner"] ?? "Olbrasoft";
            var isOwner = isAuthenticated && string.Equals(username, owner, StringComparison.OrdinalIgnoreCase);

            return Results.Ok(new
            {
                isAuthenticated,
                username,
                isOwner
            });
        });

        return app;
    }
}
