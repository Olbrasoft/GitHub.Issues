using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.GitHubId)
            .IsRequired();

        builder.Property(r => r.FullName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.HtmlUrl)
            .HasMaxLength(512)
            .IsRequired();

        builder.HasIndex(r => r.GitHubId)
            .IsUnique();

        builder.HasIndex(r => r.FullName)
            .IsUnique();
    }
}
