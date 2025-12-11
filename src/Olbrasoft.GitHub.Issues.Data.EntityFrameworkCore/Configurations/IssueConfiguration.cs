using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.RepositoryId)
            .HasColumnName("repository_id")
            .IsRequired();

        builder.Property(i => i.Number)
            .HasColumnName("number")
            .IsRequired();

        builder.Property(i => i.Title)
            .HasColumnName("title")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(i => i.IsOpen)
            .HasColumnName("is_open")
            .IsRequired();

        builder.Property(i => i.Url)
            .HasColumnName("url")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(i => i.GitHubUpdatedAt)
            .HasColumnName("github_updated_at")
            .IsRequired();

        // Embedding column - provider-specific configuration applied in DbContext.OnModelCreating
        // Required - issues without embeddings are skipped during sync (useless for semantic search)
        builder.Property(i => i.Embedding)
            .HasColumnName("embedding")
            .IsRequired();

        builder.Property(i => i.SyncedAt)
            .HasColumnName("synced_at")
            .IsRequired();

        builder.HasOne(i => i.Repository)
            .WithMany(r => r.Issues)
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.RepositoryId, i.Number })
            .IsUnique();

        // Sub-issues hierarchy (self-referencing 1:N relationship)
        builder.Property(i => i.ParentIssueId)
            .HasColumnName("parent_issue_id");

        builder.HasOne(i => i.ParentIssue)
            .WithMany(i => i.SubIssues)
            .HasForeignKey(i => i.ParentIssueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ParentIssueId);
    }
}
