using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

// Configure DbContext with secrets pattern
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dbPassword = builder.Configuration["DbPassword"];

if (!string.IsNullOrEmpty(dbPassword))
{
    connectionString += $";Password={dbPassword}";
}

builder.Services.AddDbContext<GitHubDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector());
});

// Configure settings
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<SearchSettings>(
    builder.Configuration.GetSection("Search"));

// Register process runner and service manager (required by OllamaEmbeddingService)
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IServiceManager, SystemdServiceManager>();

// Register services
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
builder.Services.AddScoped<IIssueSearchService, IssueSearchService>();

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
