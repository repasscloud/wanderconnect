using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
namespace Swoovr.Infrastructure.Persistence;
public static class PolicyNoticeMapping
{
    public static void AddPolicyNotices(this ModelBuilder model)
    {
        var publication=model.Entity<PolicyNoticePublication>();publication.ToTable("policy_notice_publications");publication.HasKey(p=>p.Id);
        publication.Property(p=>p.Version).HasMaxLength(40);publication.HasIndex(p=>p.Version).IsUnique();publication.HasIndex(p=>p.PurgeAfter);
        publication.Property(p=>p.Title).HasMaxLength(120);publication.Property(p=>p.Link).HasMaxLength(40);
        publication.ToTable(t=>t.HasCheckConstraint("ck_policy_publication","version ~ '^[A-Za-z0-9][A-Za-z0-9.-]{0,39}$' AND length(trim(title)) BETWEEN 1 AND 120 AND title !~ '[[:cntrl:]]' AND link IN ('/safety','/safety/advice') AND ((published_at IS NULL AND published_by IS NULL AND send_until IS NULL AND purge_after IS NULL AND ((disposed_at IS NULL AND staged_by IS NOT NULL) OR (disposed_at IS NOT NULL AND disposed_at>=staged_at+interval '1440 hours' AND staged_by IS NULL AND title='Expired safety update' AND link='/safety'))) OR (published_at IS NOT NULL AND send_until IS NOT NULL AND purge_after IS NOT NULL AND published_at>=staged_at AND send_until=published_at+interval '720 hours' AND purge_after=published_at+interval '1440 hours' AND ((disposed_at IS NULL AND staged_by IS NOT NULL AND published_by IS NOT NULL) OR (disposed_at IS NOT NULL AND disposed_at>=purge_after AND staged_by IS NULL AND published_by IS NULL AND title='Expired safety update' AND link='/safety'))))"));
        var recipient=model.Entity<PolicyNoticeRecipient>();recipient.ToTable("policy_notice_recipients");recipient.HasKey(r=>new{r.PublicationId,r.AccountId});
        recipient.Property(r=>r.Status).HasMaxLength(24);recipient.HasIndex(r=>new{r.PublicationId,r.Status,r.AccountId});
        recipient.HasOne<PolicyNoticePublication>().WithMany().HasForeignKey(r=>r.PublicationId).OnDelete(DeleteBehavior.Restrict);
        recipient.HasOne<Account>().WithMany().HasForeignKey(r=>r.AccountId).OnDelete(DeleteBehavior.Cascade);
        recipient.ToTable(t=>t.HasCheckConstraint("ck_policy_recipient","(status='queued' AND completed_at IS NULL) OR (status IN ('centre-recorded','suppressed') AND completed_at IS NOT NULL AND completed_at>=created_at)"));
    }
}
