using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>Call from existing OnModelCreating; then generate/review a real migration.</summary>
public static class ReportOutcomeMapping
{
    public static void AddReportOutcomes(this ModelBuilder model)
    {
        var state = model.Entity<ReportOutcomeState>();
        state.ToTable("report_outcome_states");
        state.HasKey(item => item.ReportId);
        state.Property(item => item.ReportId).HasColumnName("report_id");
        state.Property(item => item.SourceCaseId).HasColumnName("source_case_id");
        state.Property(item => item.SourceAccountId).HasColumnName("source_account_id");
        state.Property(item => item.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        state.Property(item => item.Revision).HasColumnName("revision");
        state.HasOne<Report>().WithMany().HasForeignKey(item=>new{item.ReportId,item.SourceCaseId,item.SourceAccountId})
            .HasPrincipalKey(item=>new{item.Id,item.CaseId,item.ReporterAccountId}).OnDelete(DeleteBehavior.Cascade);
        state.ToTable(t=>t.HasCheckConstraint("ck_report_outcome_checkpoint","revision>=0 AND (fingerprint IS NULL OR fingerprint ~ '^[0-9a-f]{64}$')"));
        state.HasOne<ModerationCase>().WithMany().HasForeignKey(item => item.SourceCaseId).OnDelete(DeleteBehavior.Cascade);
        state.HasIndex(item => new { item.SourceCaseId, item.SourceAccountId });
    }
}
