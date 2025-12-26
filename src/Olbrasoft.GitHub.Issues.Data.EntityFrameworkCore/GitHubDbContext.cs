using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Converters;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

public class GitHubDbContext : DbContext
{
    private readonly DatabaseSettings? _settings;

    public GitHubDbContext(DbContextOptions<GitHubDbContext> options)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public GitHubDbContext(DbContextOptions<GitHubDbContext> options, DatabaseSettings settings)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<IssueEvent> IssueEvents => Set<IssueEvent>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<TextType> TextTypes => Set<TextType>();
    public DbSet<CachedText> CachedTexts => Set<CachedText>();

    /// <summary>
    /// Gets the current database provider.
    /// </summary>
    public DatabaseProvider Provider => _settings?.Provider ?? DatabaseProvider.PostgreSQL;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations first
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GitHubDbContext).Assembly);

        // Provider-specific embedding configuration
        if (Provider == DatabaseProvider.PostgreSQL)
        {
            modelBuilder.HasPostgresExtension("vector");

            // PostgreSQL: Use pgvector type with converter from float[] to Vector
            // 768 dimensions for local Ollama nomic-embed-text
            modelBuilder.Entity<Issue>()
                .Property(e => e.Embedding)
                .HasConversion(new FloatArrayToVectorConverter())
                .HasColumnType("vector(768)");
        }
        else if (Provider == DatabaseProvider.SqlServer)
        {
            // SQL Server: Use native VECTOR type via EFCore.SqlServer.VectorSearch
            // float[] maps directly to vector with no converter needed
            // 1024 dimensions for Azure Cohere embeddings
            modelBuilder.Entity<Issue>()
                .Property(e => e.Embedding)
                .HasColumnType("vector(1024)");
        }
    }
}
