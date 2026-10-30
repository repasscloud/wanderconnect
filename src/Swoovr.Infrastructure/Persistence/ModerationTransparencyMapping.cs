using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
namespace Swoovr.Infrastructure.Persistence;

public static class ModerationTransparencyMapping
{
    public static void AddModerationTransparency(this ModelBuilder model)
    {
        var snapshot=model.Entity<ModerationTransparencySnapshot>();snapshot.ToTable("moderation_transparency_snapshots");snapshot.HasKey(s=>s.Id);
        snapshot.HasIndex(s=>new{s.EndingYear,s.Revision}).IsUnique();snapshot.HasIndex(s=>new{s.EndingYear,s.Fingerprint}).IsUnique();
        snapshot.Property(s=>s.DefinitionVersion).HasMaxLength(80);snapshot.Property(s=>s.Fingerprint).HasMaxLength(64);
        snapshot.ToTable(t=>t.HasCheckConstraint("ck_transparency_snapshot","ending_year BETWEEN 2 AND 9998 AND revision>0 AND fingerprint ~ '^[0-9a-f]{64}$' AND octet_length(aggregate_json)<=200000 AND jsonb_typeof(aggregate_json::jsonb)='object'"));
        var fact=model.Entity<ModerationTransparencyFact>();fact.ToTable("moderation_transparency_facts");fact.HasKey(f=>f.Id);
        fact.HasIndex(f=>new{f.SourceKind,f.SourceId}).IsUnique();fact.HasIndex(f=>new{f.ActionAt,f.Environment});
        fact.HasIndex(f=>new{f.SourceCaseId,f.SubjectAccountId});
        fact.Property(f=>f.SourceKind).HasMaxLength(24);fact.Property(f=>f.Action).HasMaxLength(24);
        fact.Property(f=>f.HomeCountryAtAction).HasMaxLength(8);fact.Property(f=>f.CountryProvenance).HasMaxLength(40);
        fact.Property(f=>f.ReviewedPolicyBasis).HasMaxLength(32);fact.Property(f=>f.DetectionMechanism).HasMaxLength(24);
        fact.Property(f=>f.EnforcementMechanism).HasMaxLength(24);fact.Property(f=>f.AutomationDefinition).HasMaxLength(40);
        fact.Property(f=>f.Initiative).HasMaxLength(24);fact.Property(f=>f.Environment).HasMaxLength(24);
        // Deliberately no cascading FK to source/account/case: independent receipt retention must survive source loss.
        // Deferred SQL provenance guard below checks the original positive relationship at insertion.
        fact.ToTable(t=>
        {
            t.HasCheckConstraint("ck_transparency_fact_source","id <> '00000000-0000-0000-0000-000000000000'::uuid AND source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND subject_account_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (source_case_id IS NULL OR source_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((source_kind='sanction' AND action='termination') OR (source_kind IN ('content-removal','media-decision') AND action='content-removal')) AND recorded_at >= action_at");
            t.HasCheckConstraint("ck_transparency_fact_country","(country_provenance='member-declared-home-at-action' AND home_country_at_action IN ('AU','other')) OR (country_provenance='unknown' AND home_country_at_action='unknown')");
            t.HasCheckConstraint("ck_transparency_fact_basis","reviewed_policy_basis IN ('unknown','harassment','threats-violence','sexual-abuse','child-safety','fraud-scam','hate-discrimination','privacy','other-reviewed') AND basis_reviewed=(reviewed_policy_basis<>'unknown')");
            t.HasCheckConstraint("ck_transparency_fact_mechanisms","detection_mechanism IN ('unknown','member-report','staff-review','local-text-rules','image-screening') AND enforcement_mechanism IN ('unknown','human','automated-screening') AND initiative IN ('unknown','reported','own-initiative') AND automation_definition IN ('none','unknown','local-text-rules-v1','image-screening-unversioned') AND environment IN ('unknown','production','local-synthetic')");
        });
    }
}
