using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

// Configure DbContext
builder.Services.AddDbContext<GitHubDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseVector());
});

// Configure embedding settings
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection("Embeddings"));

// Register services
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IssueSearchService>();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
