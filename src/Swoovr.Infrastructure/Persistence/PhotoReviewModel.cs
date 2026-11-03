using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

public static class PhotoReviewModel
{
    public static void AddPhotoReviewWorkflows(this ModelBuilder model)
    {
        model.Entity<MediaSafetyAcknowledgement>(e =>
        {
            e.ToTable("media_safety_acknowledgements");
            e.HasKey(x => new { x.StaffAccountId, x.Version }); e.Property(x => x.Version).HasMaxLength(32);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.StaffAccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MediaDecisionRecord>(e =>
        {
            e.ToTable("media_decision_records");
            e.Property(x => x.Decision).HasMaxLength(32); e.Property(x => x.Code).HasMaxLength(64); e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.AccountId, x.At }); e.HasIndex(x => new { x.MediaId, x.At });
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MediaAppeal>(e =>
        {
            e.ToTable("media_appeals");
            e.Property(x => x.Text).HasMaxLength(1000); e.Property(x => x.Outcome).HasMaxLength(32);
            e.HasIndex(x => x.DecisionId).IsUnique(); e.HasIndex(x => new { x.DecidedAt, x.SubmittedAt });
            e.HasOne<MediaDecisionRecord>().WithMany().HasForeignKey(x => x.DecisionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
