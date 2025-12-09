using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

public class GitHubDbContext : DbContext
{
    public GitHubDbContext(DbContextOptions<GitHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Issue> Issues => Set<Issue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GitHubDbContext).Assembly);
    }
}
