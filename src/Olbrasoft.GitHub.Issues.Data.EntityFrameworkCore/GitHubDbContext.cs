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
    }

    public GitHubDbContext(DbContextOptions<GitHubDbContext> options, DatabaseSettings settings)
        : base(options)
    {
        _settings = settings;
    }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<IssueEvent> IssueEvents => Set<IssueEvent>();

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

            // PostgreSQL: Use native pgvector type
            modelBuilder.Entity<Issue>()
                .Property(e => e.Embedding)
                .HasColumnType("vector(768)");
        }
        else if (Provider == DatabaseProvider.SqlServer)
        {
            // SQL Server: Store as binary with value converter
            // Native VECTOR type exists but no EF Core mapping yet
            modelBuilder.Entity<Issue>()
                .Property(e => e.Embedding)
                .HasColumnType("varbinary(max)")
                .HasConversion(new VectorToBinaryConverter());
        }
    }
}
