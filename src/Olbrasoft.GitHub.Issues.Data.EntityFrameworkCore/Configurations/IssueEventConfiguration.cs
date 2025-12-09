using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class IssueEventConfiguration : IEntityTypeConfiguration<IssueEvent>
{
    public void Configure(EntityTypeBuilder<IssueEvent> builder)
    {
        builder.ToTable("issue_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.GitHubEventId)
            .HasColumnName("github_event_id")
            .IsRequired();

        builder.Property(e => e.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(e => e.EventTypeId)
            .HasColumnName("event_type_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ActorId)
            .HasColumnName("actor_id");

        builder.Property(e => e.ActorLogin)
            .HasColumnName("actor_login")
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
