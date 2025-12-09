using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("repositories");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.GitHubId)
            .HasColumnName("github_id")
            .IsRequired();

        builder.Property(r => r.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.HtmlUrl)
            .HasColumnName("html_url")
            .HasMaxLength(512)
            .IsRequired();

        builder.HasIndex(r => r.GitHubId)
            .IsUnique();

        builder.HasIndex(r => r.FullName)
            .IsUnique();
    }
}
