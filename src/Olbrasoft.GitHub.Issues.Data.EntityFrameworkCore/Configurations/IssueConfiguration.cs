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

        builder.Property(i => i.State)
            .HasColumnName("state")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.HtmlUrl)
            .HasColumnName("html_url")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(i => i.GitHubUpdatedAt)
            .HasColumnName("github_updated_at")
            .IsRequired();

        builder.Property(i => i.TitleEmbedding)
            .HasColumnName("title_embedding")
            .HasColumnType("vector(1536)");

        builder.Property(i => i.SummaryCz)
            .HasColumnName("summary_cz")
            .HasMaxLength(2048);

        builder.Property(i => i.SummaryCzEmbedding)
            .HasColumnName("summary_cz_embedding")
            .HasColumnType("vector(1536)");

        builder.Property(i => i.SyncedAt)
            .HasColumnName("synced_at")
            .IsRequired();

        builder.HasOne(i => i.Repository)
            .WithMany(r => r.Issues)
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.RepositoryId, i.Number })
            .IsUnique();
    }
}
