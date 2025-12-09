using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(l => l.Name)
            .IsUnique();
    }
}
