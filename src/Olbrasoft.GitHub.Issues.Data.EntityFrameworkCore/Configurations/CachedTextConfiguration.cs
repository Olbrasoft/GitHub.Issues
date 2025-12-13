using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class CachedTextConfiguration : IEntityTypeConfiguration<CachedText>
{
    public void Configure(EntityTypeBuilder<CachedText> builder)
    {
        // Composite primary key
        builder.HasKey(t => new { t.LanguageId, t.TextTypeId, t.IssueId });

        builder.Property(t => t.Content)
            .IsRequired();

        builder.Property(t => t.CachedAt)
            .IsRequired();

        // Language is a lookup table - RESTRICT delete
        builder.HasOne(t => t.Language)
            .WithMany(l => l.CachedTexts)
            .HasForeignKey(t => t.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        // TextType is a lookup table - RESTRICT delete
        builder.HasOne(t => t.TextType)
            .WithMany(tt => tt.CachedTexts)
            .HasForeignKey(t => t.TextTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Issue deletion cascades to cached texts
        builder.HasOne(t => t.Issue)
            .WithMany(i => i.CachedTexts)
            .HasForeignKey(t => t.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on IssueId for fast lookups and cache invalidation
        builder.HasIndex(t => t.IssueId);
    }
}
