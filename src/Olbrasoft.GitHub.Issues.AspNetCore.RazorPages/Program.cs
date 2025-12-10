using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

// Settings classes: GitHubSettings, BodyPreviewSettings, SearchSettings are in Services namespace

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

// Configure database provider from settings
var databaseSettings = builder.Configuration.GetSection("Database").Get<DatabaseSettings>()
    ?? new DatabaseSettings { Provider = DatabaseProvider.PostgreSQL };

// Build connection string with secrets pattern
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dbPassword = builder.Configuration["DbPassword"];

if (!string.IsNullOrEmpty(dbPassword))
{
    connectionString += $";Password={dbPassword}";
}

// Register DbContext with multi-provider support
builder.Services.AddGitHubDbContext(connectionString!, databaseSettings.Provider);

// Configure settings
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<SearchSettings>(
    builder.Configuration.GetSection("Search"));
builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<BodyPreviewSettings>(
    builder.Configuration.GetSection("BodyPreview"));
builder.Services.Configure<AiProvidersSettings>(
    builder.Configuration.GetSection("AiProviders"));
builder.Services.Configure<SummarizationSettings>(
    builder.Configuration.GetSection("Summarization"));

// Register process runner and service manager (required by OllamaEmbeddingService)
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IServiceManager, SystemdServiceManager>();

// Register services
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddHttpClient<GitHubGraphQLClient>();
builder.Services.AddHttpClient<RotatingAiSummarizationService>();
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OllamaEmbeddingService>());
builder.Services.AddScoped<IGitHubGraphQLClient>(sp => sp.GetRequiredService<GitHubGraphQLClient>());
builder.Services.AddScoped<IAiSummarizationService>(sp => sp.GetRequiredService<RotatingAiSummarizationService>());
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
