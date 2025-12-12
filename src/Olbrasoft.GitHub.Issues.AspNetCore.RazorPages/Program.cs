using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add core services
builder.Services.AddRazorPages();
builder.Services.AddSignalR(options =>
{
    // Increase timeouts to prevent disconnection on Azure/proxy environments
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
});

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".GitHubIssues.Session";
});

// Configure services using extension methods
builder.Services.AddGitHubAuthentication(builder.Configuration);
builder.Services.AddGitHubDatabase(builder.Configuration);
builder.Services.AddGitHubServices(builder.Configuration);
builder.Services.AddGitHubHttpClients(builder.Configuration);

var app = builder.Build();

// Apply pending database migrations on startup
await app.ApplyMigrationsAsync();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapRazorPages();
app.MapHub<IssueUpdatesHub>("/hubs/issues");
app.MapAuthEndpoints();
app.MapRepositoryEndpoints();
app.MapDatabaseEndpoints();
app.MapIssueEndpoints();
app.MapSyncEndpoints();
app.MapWebhookEndpoints();

app.Run();
