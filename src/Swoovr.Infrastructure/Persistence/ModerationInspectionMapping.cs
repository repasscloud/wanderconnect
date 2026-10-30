using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
namespace Swoovr.Infrastructure.Persistence;

public static class ModerationInspectionMapping
{
    public static void AddModerationInspectionTotals(this ModelBuilder model)
    {
        var row=model.Entity<ModerationInspectionTotal>();row.ToTable("moderation_inspection_totals");
        row.HasKey(r=>new{r.Day,r.Environment,r.Definition});
        row.Property(r=>r.Environment).HasMaxLength(24);row.Property(r=>r.Definition).HasMaxLength(40);
        row.ToTable(t=>
        {
            t.HasCheckConstraint("ck_inspection_total_dimensions","environment IN ('unknown','production','local-synthetic') AND definition IN ('local-text-rules-v1','image-screening-unversioned')");
            t.HasCheckConstraint("ck_inspection_total_counts","completed_inspections>=0 AND incomplete_history>=0 AND incomplete_history<=completed_inspections AND created_candidates>=0 AND created_candidates<=completed_inspections AND failed_processing_attempts>=0 AND (definition<>'image-screening-unversioned' OR incomplete_history=0)");
        });
        // No account, case, content, contact, country or source receipt foreign key/column.
        // Existing final snake_case pass owns column naming; raw UPSERT uses those exact names.
    }
}
