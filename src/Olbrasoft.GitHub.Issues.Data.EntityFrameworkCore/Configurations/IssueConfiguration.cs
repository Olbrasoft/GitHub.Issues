using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.RepositoryId)
            .IsRequired();

        builder.Property(i => i.Number)
            .IsRequired();

        builder.Property(i => i.Title)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(i => i.IsOpen)
            .IsRequired();

        builder.Property(i => i.Url)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(i => i.GitHubUpdatedAt)
            .IsRequired();

        // Embedding column - provider-specific configuration applied in DbContext.OnModelCreating
        // Optional - issues can be synced without embeddings if embedding service fails
        // Issues without embeddings won't appear in semantic search but can still be found by title/number
        builder.Property(i => i.Embedding);

        builder.Property(i => i.SyncedAt)
            .IsRequired();

        builder.HasOne(i => i.Repository)
            .WithMany(r => r.Issues)
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.RepositoryId, i.Number })
            .IsUnique();

        // Sub-issues hierarchy (self-referencing 1:N relationship)
        builder.HasOne(i => i.ParentIssue)
            .WithMany(i => i.SubIssues)
            .HasForeignKey(i => i.ParentIssueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ParentIssueId);
    }
}
