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

        builder.Property(i => i.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.Url)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(i => i.GitHubUpdatedAt)
            .IsRequired();

        // !!! CRITICAL: Embedding is REQUIRED - DO NOT make this nullable !!!
        // !!! Without embedding the record is useless - we search by embedding !!!
        // !!! Records without embedding cannot be found in semantic search !!!
        // Provider-specific vector type configuration is applied in DbContext.OnModelCreating
        builder.Property(i => i.Embedding)
            .IsRequired();

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

        // Index for efficient filtering of non-deleted issues
        builder.HasIndex(i => i.IsDeleted);
    }
}
