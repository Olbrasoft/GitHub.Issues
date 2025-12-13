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

        builder.Property(l => l.EnglishName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.NativeName)
            .HasMaxLength(100);

        builder.Property(l => l.TwoLetterISOCode)
            .HasMaxLength(2);

        builder.HasIndex(l => l.CultureName)
            .IsUnique();

        // Seed initial languages with LCID values from CultureInfo
        builder.HasData(
            new Language
            {
                Id = 1029,  // CultureInfo("cs-CZ").LCID
                CultureName = "cs-CZ",
                EnglishName = "Czech (Czechia)",
                NativeName = "čeština (Česko)",
                TwoLetterISOCode = "cs"
            },
            new Language
            {
                Id = 1031,  // CultureInfo("de-DE").LCID
                CultureName = "de-DE",
                EnglishName = "German (Germany)",
                NativeName = "Deutsch (Deutschland)",
                TwoLetterISOCode = "de"
            },
            new Language
            {
                Id = 1033,  // CultureInfo("en-US").LCID
                CultureName = "en-US",
                EnglishName = "English (United States)",
                NativeName = "English (United States)",
                TwoLetterISOCode = "en"
            }
        );
    }
}
