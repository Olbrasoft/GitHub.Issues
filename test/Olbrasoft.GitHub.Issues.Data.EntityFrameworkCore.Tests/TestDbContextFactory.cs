using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Factory for creating in-memory GitHubDbContext instances for testing.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new in-memory database context for testing.
    /// Each call creates a new isolated database instance.
    /// </summary>
    public static TestGitHubDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TestGitHubDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new TestGitHubDbContext(options);
    }
}

/// <summary>
/// Test-specific DbContext that handles in-memory database limitations.
/// Inherits from GitHubDbContext for compatibility with handlers.
/// </summary>
public class TestGitHubDbContext : GitHubDbContext
{
    public TestGitHubDbContext(DbContextOptions options)
        : base(CreateGitHubDbContextOptions(options))
    {
    }

    private static DbContextOptions<GitHubDbContext> CreateGitHubDbContextOptions(DbContextOptions options)
    {
        var builder = new DbContextOptionsBuilder<GitHubDbContext>();

        // Get database name from original options extension
        foreach (var ext in options.Extensions)
        {
            if (ext.GetType().Name.Contains("InMemory"))
            {
                var storeNameProp = ext.GetType().GetProperty("StoreName");
                var storeName = storeNameProp?.GetValue(ext) as string ?? Guid.NewGuid().ToString();
                builder.UseInMemoryDatabase(storeName);
                break;
            }
        }

        return builder.Options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entities manually without the Vector type complications
        // Repository configuration
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.FullName).IsRequired().HasMaxLength(256);
            entity.Property(r => r.GitHubId).IsRequired();
            entity.Property(r => r.HtmlUrl).IsRequired().HasMaxLength(512);
            entity.HasIndex(r => r.FullName).IsUnique();
        });

        // Issue configuration - Embedding is just float[] for in-memory
        modelBuilder.Entity<Issue>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.RepositoryId).IsRequired();
            entity.Property(i => i.Number).IsRequired();
            entity.Property(i => i.Title).HasMaxLength(1024).IsRequired();
            entity.Property(i => i.IsOpen).IsRequired();
            entity.Property(i => i.Url).HasMaxLength(512).IsRequired();
            entity.Property(i => i.GitHubUpdatedAt).IsRequired();
            entity.Property(i => i.Embedding); // Simple float[] for in-memory
            entity.Property(i => i.SyncedAt).IsRequired();
            entity.HasOne(i => i.Repository).WithMany(r => r.Issues).HasForeignKey(i => i.RepositoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(i => new { i.RepositoryId, i.Number }).IsUnique();
            entity.HasOne(i => i.ParentIssue).WithMany(i => i.SubIssues).HasForeignKey(i => i.ParentIssueId).OnDelete(DeleteBehavior.Restrict);
        });

        // Label configuration
        modelBuilder.Entity<Label>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.RepositoryId).IsRequired();
            entity.Property(l => l.Name).HasMaxLength(256).IsRequired();
            entity.Property(l => l.Color).HasMaxLength(6).HasDefaultValue("ededed").IsRequired();
            entity.HasOne(l => l.Repository).WithMany(r => r.Labels).HasForeignKey(l => l.RepositoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(l => new { l.RepositoryId, l.Name }).IsUnique();
        });

        // IssueLabel configuration
        modelBuilder.Entity<IssueLabel>(entity =>
        {
            entity.HasKey(il => new { il.IssueId, il.LabelId });
            entity.HasOne(il => il.Issue).WithMany(i => i.IssueLabels).HasForeignKey(il => il.IssueId);
            entity.HasOne(il => il.Label).WithMany(l => l.IssueLabels).HasForeignKey(il => il.LabelId);
        });

        // EventType configuration
        modelBuilder.Entity<EventType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // IssueEvent configuration
        modelBuilder.Entity<IssueEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitHubEventId).IsRequired();
            entity.Property(e => e.IssueId).IsRequired();
            entity.Property(e => e.EventTypeId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ActorLogin).HasMaxLength(100);
            entity.HasIndex(e => e.GitHubEventId).IsUnique();
            entity.HasIndex(e => e.IssueId);
            entity.HasOne(e => e.Issue).WithMany(i => i.Events).HasForeignKey(e => e.IssueId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.EventType).WithMany(et => et.IssueEvents).HasForeignKey(e => e.EventTypeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
