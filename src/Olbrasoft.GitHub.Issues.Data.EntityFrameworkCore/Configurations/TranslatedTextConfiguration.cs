using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class TranslatedTextConfiguration : IEntityTypeConfiguration<TranslatedText>
{
    public void Configure(EntityTypeBuilder<TranslatedText> builder)
    {
        // Composite primary key
        builder.HasKey(t => new { t.LanguageId, t.TextTypeId, t.IssueId });

        builder.Property(t => t.Content)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Language is a lookup table - RESTRICT delete
        // Prevents accidental deletion of languages that have translations
        builder.HasOne(t => t.Language)
            .WithMany(l => l.TranslatedTexts)
            .HasForeignKey(t => t.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        // TextType is a lookup table - RESTRICT delete
        // Prevents accidental deletion of text types that have translations
        builder.HasOne(t => t.TextType)
            .WithMany(tt => tt.TranslatedTexts)
            .HasForeignKey(t => t.TextTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Issue deletion cascades to translations
        // When an issue is deleted, all its cached translations are automatically deleted
        builder.HasOne(t => t.Issue)
            .WithMany(i => i.TranslatedTexts)
            .HasForeignKey(t => t.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on IssueId for fast lookups and cache invalidation
        builder.HasIndex(t => t.IssueId);
    }
}
