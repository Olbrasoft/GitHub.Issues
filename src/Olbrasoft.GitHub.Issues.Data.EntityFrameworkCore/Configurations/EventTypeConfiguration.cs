using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable("event_types");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => e.Name)
            .IsUnique();

        // Seed all 41 GitHub event types
        builder.HasData(
            new EventType { Id = 1, Name = "assigned" },
            new EventType { Id = 2, Name = "automatic_base_change_failed" },
            new EventType { Id = 3, Name = "automatic_base_change_succeeded" },
            new EventType { Id = 4, Name = "base_ref_changed" },
            new EventType { Id = 5, Name = "closed" },
            new EventType { Id = 6, Name = "commented" },
            new EventType { Id = 7, Name = "committed" },
            new EventType { Id = 8, Name = "connected" },
            new EventType { Id = 9, Name = "convert_to_draft" },
            new EventType { Id = 10, Name = "converted_to_discussion" },
            new EventType { Id = 11, Name = "cross-referenced" },
            new EventType { Id = 12, Name = "demilestoned" },
            new EventType { Id = 13, Name = "deployed" },
            new EventType { Id = 14, Name = "deployment_environment_changed" },
            new EventType { Id = 15, Name = "disconnected" },
            new EventType { Id = 16, Name = "head_ref_deleted" },
            new EventType { Id = 17, Name = "head_ref_restored" },
            new EventType { Id = 18, Name = "head_ref_force_pushed" },
            new EventType { Id = 19, Name = "labeled" },
            new EventType { Id = 20, Name = "locked" },
            new EventType { Id = 21, Name = "mentioned" },
            new EventType { Id = 22, Name = "marked_as_duplicate" },
            new EventType { Id = 23, Name = "merged" },
            new EventType { Id = 24, Name = "milestoned" },
            new EventType { Id = 25, Name = "pinned" },
            new EventType { Id = 26, Name = "ready_for_review" },
            new EventType { Id = 27, Name = "referenced" },
            new EventType { Id = 28, Name = "renamed" },
            new EventType { Id = 29, Name = "reopened" },
            new EventType { Id = 30, Name = "review_dismissed" },
            new EventType { Id = 31, Name = "review_requested" },
            new EventType { Id = 32, Name = "review_request_removed" },
            new EventType { Id = 33, Name = "reviewed" },
            new EventType { Id = 34, Name = "subscribed" },
            new EventType { Id = 35, Name = "transferred" },
            new EventType { Id = 36, Name = "unassigned" },
            new EventType { Id = 37, Name = "unlabeled" },
            new EventType { Id = 38, Name = "unlocked" },
            new EventType { Id = 39, Name = "unmarked_as_duplicate" },
            new EventType { Id = 40, Name = "unpinned" },
            new EventType { Id = 41, Name = "unsubscribed" },
            new EventType { Id = 42, Name = "user_blocked" }
        );
    }
}
