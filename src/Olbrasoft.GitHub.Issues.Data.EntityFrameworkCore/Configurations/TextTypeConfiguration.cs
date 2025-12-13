using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class TextTypeConfiguration : IEntityTypeConfiguration<TextType>
{
    public void Configure(EntityTypeBuilder<TextType> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.Name)
            .IsUnique();

        // Seed text types matching TextTypeCode enum values
        builder.HasData(
            new TextType { Id = 1, Name = "Title" },
            new TextType { Id = 2, Name = "ListSummary" },
            new TextType { Id = 3, Name = "DetailSummary" }
        );
    }
}
