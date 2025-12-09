using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Configurations;

public class IssueLabelConfiguration : IEntityTypeConfiguration<IssueLabel>
{
    public void Configure(EntityTypeBuilder<IssueLabel> builder)
    {
        builder.ToTable("issue_labels");

        builder.HasKey(il => new { il.IssueId, il.LabelId });

        builder.Property(il => il.IssueId)
            .HasColumnName("issue_id");

        builder.Property(il => il.LabelId)
            .HasColumnName("label_id");

        builder.HasOne(il => il.Issue)
            .WithMany(i => i.IssueLabels)
            .HasForeignKey(il => il.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(il => il.Label)
            .WithMany(l => l.IssueLabels)
            .HasForeignKey(il => il.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
