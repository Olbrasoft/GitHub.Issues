using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.RepositoryId)
            .IsRequired();

        builder.Property(l => l.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.Color)
            .HasMaxLength(6)
            .HasDefaultValue("ededed")
            .IsRequired();

        builder.HasOne(l => l.Repository)
            .WithMany(r => r.Labels)
            .HasForeignKey(l => l.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.RepositoryId, l.Name })
            .IsUnique();
    }
}
