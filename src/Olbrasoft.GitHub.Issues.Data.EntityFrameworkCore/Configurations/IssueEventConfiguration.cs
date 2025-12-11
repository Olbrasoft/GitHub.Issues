using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class IssueEventConfiguration : IEntityTypeConfiguration<IssueEvent>
{
    public void Configure(EntityTypeBuilder<IssueEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.GitHubEventId)
            .IsRequired();

        builder.Property(e => e.IssueId)
            .IsRequired();

        builder.Property(e => e.EventTypeId)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.ActorLogin)
            .HasMaxLength(100);

        builder.HasIndex(e => e.GitHubEventId)
            .IsUnique();

        builder.HasIndex(e => e.IssueId);

        builder.HasOne(e => e.Issue)
            .WithMany(i => i.Events)
            .HasForeignKey(e => e.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EventType)
            .WithMany(et => et.IssueEvents)
            .HasForeignKey(e => e.EventTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
