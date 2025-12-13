using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        // LCID as primary key - NOT auto-generated
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedNever();

        builder.Property(l => l.CultureName)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(l => l.CultureName)
            .IsUnique();

        // Seed initial languages with LCID values from CultureInfo
        // Use CultureInfo.GetCultureInfo(Id) to get EnglishName, NativeName, etc.
        builder.HasData(
            new Language { Id = 1029, CultureName = "cs-CZ" },  // Czech
            new Language { Id = 1031, CultureName = "de-DE" },  // German
            new Language { Id = 1033, CultureName = "en-US" }   // English
        );
    }
}
